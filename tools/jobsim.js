#!/usr/bin/env node
// Pacing check for docs/specs/jobs-m4.md (danfo conductor shift).
// Same numbers as the spec's section 6 and the server draft in section 9.
// Run: node tools/jobsim.js
'use strict';
const SEAT_PAY = 60, FULL_BONUS = 200, SEATS = 14, WRONG_COST = 100, FARES = 4;
const MIN_TRIP_S = 150, SHIFT_MAX_TRIPS = 6, SHIFTS_PER_DAY = 3;
const levelFor = (trips) => Math.min(10, 1 + Math.floor(Math.sqrt(trips / 25)));
const mult = (lvl) => 0.65 + 0.085 * (lvl - 1);
const tripPay = (seats, right, lvl) => {
  if (seats < 4) return 0;
  const base = SEAT_PAY * seats + (seats >= SEATS ? FULL_BONUS : 0) - WRONG_COST * (FARES - right);
  return Math.max(0, Math.round((base * mult(lvl)) / 10) * 10);
};
const shiftCap = (lvl) => SHIFT_MAX_TRIPS * tripPay(SEATS, FARES, lvl);

// A steady player: 3 shifts a day, 5 trips a shift (a 180 s trip cycle in 15 min),
// 12 of 14 seats on average, 3 of 4 fares right.
function run(days, seats, right, tripsPerShift) {
  let trips = 0, money = 0;
  const marks = {};
  for (let d = 1; d <= days; d++) {
    for (let s = 0; s < SHIFTS_PER_DAY; s++) {
      let pay = 0;
      for (let t = 0; t < tripsPerShift; t++) { pay += tripPay(seats, right, levelFor(trips)); trips++; }
      money += Math.min(pay, shiftCap(levelFor(trips)));
    }
    if ([1, 7, 30, 60, 90, 180, 365].includes(d)) marks[d] = { level: levelFor(trips), total: money, perDay: 0 };
  }
  return marks;
}
console.log('Trip pay, full bus + all change right, by level:');
for (const l of [1, 2, 3, 5, 10]) console.log(`  L${l}: N${tripPay(14, 4, l)}  shift cap N${shiftCap(l)}`);
console.log('Worst cheater (always full, always right, 6 trips, 3 shifts) at L1: N' + 3 * shiftCap(1) + ' a day');
for (const [name, args] of [['steady', [12, 3, 5]], ['perfect', [14, 4, 6]]]) {
  console.log(`\n${name} player (seats ${args[0]}, right ${args[1]}/4, ${args[2]} trips a shift):`);
  const m = run(365, ...args);
  for (const d of Object.keys(m)) console.log(`  day ${d}: level ${m[d].level}, earned N${m[d].total.toLocaleString('en-NG')}`);
}
console.log('\nDays of steady conductor work for the N30M mansion deposit: ' +
  Math.round(30e6 / (run(365, 12, 3, 5)[365].total / 365)));
