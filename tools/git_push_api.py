#!/usr/bin/env python3
"""Push local git commits to GitHub via the Git Data API.

Replicates `git push origin main` for commits that exist locally but
cannot be pushed over HTTPS (no raw token available). Uses the
custom.github vault credential via authd surrogates.

Usage: git_push_api.py <repo> <local-base-sha> <commit-sha> [<commit-sha> ...]
  repo: e.g. benjy659/naija-life-unity
  local-base-sha: the remote HEAD the local commits are based on (fast-forward check)
  commit-sha: one or more local commit SHAs to replicate, in order
"""
import json
import subprocess
import sys
import urllib.request
import urllib.error
import http.client

sys.path.insert(0, "/opt/muse/skills/custom-connector-setup/bin")
from dynamic_credentials import dynamic_credential_entry, add_surrogate_to_request, read_json_response

API = "https://api.github.com"


def git(*args, cwd):
    r = subprocess.run(["git"] + list(args), cwd=cwd, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {r.stderr[:500]}")
    return r.stdout


def api_request(method, path, data=None, retries=4):
    import time
    last_err = None
    for attempt in range(retries):
        url = API + path
        body = json.dumps(data).encode() if data is not None else None
        req = urllib.request.Request(url, data=body, method=method)
        req.add_header("Accept", "application/vnd.github+json")
        req.add_header("X-GitHub-Api-Version", "2022-11-28")
        if body:
            req.add_header("Content-Type", "application/json")
        add_surrogate_to_request(req, "custom.github", allowed_hosts=["api.github.com"])
        try:
            resp = urllib.request.urlopen(req, timeout=60)
            return read_json_response(resp)
        except urllib.error.HTTPError as e:
            err_body = e.read().decode()[:1500]
            # Don't retry 4xx (client error) except 429
            if e.code == 429 or (500 <= e.code < 600):
                last_err = e
                wait = 2 ** attempt
                print(f"HTTP {e.code} on {method} {path}, retrying in {wait}s...", file=sys.stderr)
                time.sleep(wait)
                continue
            print(f"HTTP {e.code} on {method} {path}\nResponse: {err_body}", file=sys.stderr)
            raise
        except (urllib.error.URLError, http.client.RemoteDisconnected, ConnectionError) as e:
            last_err = e
            wait = 2 ** attempt
            print(f"Connection error on {method} {path}: {e}, retrying in {wait}s...", file=sys.stderr)
            time.sleep(wait)
            continue
    raise last_err


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(2)
    repo, base_sha, commits = sys.argv[1], sys.argv[2], sys.argv[3:]
    cwd = "/home/hatch/workspace/naija-life-unity"

    # Fast-forward check: remote main must still be at base_sha
    ref = api_request("GET", f"/repos/{repo}/git/refs/heads/main")
    remote_sha = ref["object"]["sha"]
    if remote_sha != base_sha:
        print(f"ABORT: remote main is at {remote_sha}, expected {base_sha} (not fast-forward)")
        sys.exit(1)
    print(f"Remote main confirmed at {base_sha}")

    parent_sha = base_sha
    for sha in commits:
        # Commit metadata
        msg = git("log", "-1", "--format=%B", sha, cwd=cwd).strip()
        author_name = git("log", "-1", "--format=%an", sha, cwd=cwd).strip()
        author_email = git("log", "-1", "--format=%ae", sha, cwd=cwd).strip()
        author_date = git("log", "-1", "--format=%aI", sha, cwd=cwd).strip()

        # Changed files (added/modified; handle deletions too)
        diff = git("diff-tree", "--no-commit-id", "--name-status", "-r", "--root", sha, cwd=cwd)
        tree_items = []
        blob_count = 0
        for line in diff.strip().split("\n"):
            if not line.strip():
                continue
            status, path = line.split("\t", 1)
            if status.startswith("D"):
                tree_items.append({"path": path, "mode": "100644", "type": "blob", "sha": None})
            else:
                content = subprocess.run(
                    ["git", "show", f"{sha}:{path}"], cwd=cwd,
                    capture_output=True
                )
                if content.returncode != 0:
                    # Could be a submodule or special file; skip with warning
                    print(f"  WARN: cannot read {path}, skipping")
                    continue
                import base64
                b64 = base64.b64encode(content.stdout).decode()
                blob = api_request("POST", f"/repos/{repo}/git/blobs",
                                   {"content": b64, "encoding": "base64"})
                tree_items.append({"path": path, "mode": "100644", "type": "blob",
                                   "sha": blob["sha"]})
                blob_count += 1
        print(f"Commit {sha[:7]}: {blob_count} blobs created, {len(tree_items)} tree entries")

        # Get parent tree SHA
        parent_commit = api_request("GET", f"/repos/{repo}/git/commits/{parent_sha}")
        base_tree = parent_commit["tree"]["sha"]

        # Create tree (None sha = deletion)
        tree_data = {"base_tree": base_tree,
                     "tree": [{k: v for k, v in it.items() if v is not None} for it in tree_items]}
        # For deletions, the API wants sha: null explicitly
        for orig, new in zip(tree_items, tree_data["tree"]):
            if orig["sha"] is None:
                new["sha"] = None
        new_tree = api_request("POST", f"/repos/{repo}/git/trees", tree_data)

        # Create commit
        new_commit = api_request("POST", f"/repos/{repo}/git/commits", {
            "message": msg,
            "tree": new_tree["sha"],
            "parents": [parent_sha],
            "author": {"name": author_name, "email": author_email, "date": author_date},
        })
        print(f"Commit {sha[:7]} replicated as {new_commit['sha'][:7]}")
        parent_sha = new_commit["sha"]

    # Update the ref (fast-forward)
    api_request("PATCH", f"/repos/{repo}/git/refs/heads/main", {"sha": parent_sha})
    print(f"refs/heads/main updated to {parent_sha[:7]}")
    print("PUSH COMPLETE")


if __name__ == "__main__":
    main()
