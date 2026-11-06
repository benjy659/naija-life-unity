// Naija Streets (Unity rebuild) — NLStrings
// Every player-facing string in the HUD and phone lives here, so later
// translation and review is one file. Spec: docs/specs/hud-and-phone.md
// section 11. Short, lively, Nigerian flavour, light Pidgin where it fits.
// No raw separators, no debug text, no paragraphs.

namespace NaijaLife.UI
{
    public static class NLStrings
    {
        // HUD
        public const string AppName = "Naija Streets";
        public const string WalletTitle = "Your wallet";
        public const string NextPrefix = "Next";
        public const string MenuHintDesktop = "P Phone   M Map   V Camera   Esc Menu";
        public const string VitalsHint = "Needs open after the survival loop lands";
        public const string MapStubTitle = "Full map lands after M2";
        public const string MapStubLine = "Oshodi greybox is next. Your radar still works.";
        public const string NoObjective = "No job yet. Your first shift go show here.";

        // Phone
        public const string PhoneBank = "Bank";
        public const string PhoneMap = "Map";
        public const string PhoneSettings = "Settings";
        public const string BankYourWallet = "YOUR WALLET";
        public const string BankHistory = "History";
        public const string BankThisWeek = "This week";
        public const string BankMoneyIn = "Money in";
        public const string BankMoneyOut = "Money out";
        public const string BankNet = "Net";
        public const string BankSynced = "Synced {0} ago";
        public const string BankRefresh = "Refresh";
        public const string BankLoadMore = "Load more";
        public const string BankLockNote = "Every naira here na game money. E no fit cash out.";
        public const string BankLoading = "Still loading your money...";
        public const string BankOffline = "No network. Showing your last balance.";
        public const string BankRetry = "Retry";
        public const string BankEmpty = "No money moves yet. Your first shift go show here.";
        // Send tab stays hidden until B-38 is owner-approved (spec section 8.3).
        public const string BankSendLocked = "Sending money between players is not available yet.";

        // Settings
        public const string SettingsReduceMotion = "Reduce motion";
        public const string SettingsReduceMotionHint = "Turns off slides and count-ups";
        public const string SettingsSprintToggle = "Tap sprint to toggle";
        public const string SettingsSprintToggleHint = "Off means hold sprint to run";
        public const string SettingsKeyHints = "Show key hints";
        public const string SettingsTitle = "Settings";

        // Prompts and toasts
        public const string PromptMore = "+{0}";
        public const string ToastShiftPay = "Shift pay landed";
        public const string ToastWelcome = "Welcome to Naija Streets";
        public const string ToastWelcomeLine = "Oshodi dey wait for you";
    }
}
