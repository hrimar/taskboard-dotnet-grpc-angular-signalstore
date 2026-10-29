import { Timestamp } from '../../core/proto-gen/google/protobuf/timestamp';

// The user picks a calendar DATE, not an instant. The server stores UTC, so the date is sent as
// UTC midnight of the chosen day and read back with UTC getters. Going through local time in
// either direction would shift the day for users west/east of UTC (e.g. local midnight in UTC+3
// is the previous day in UTC).

// The datepicker yields a local-midnight Date: take its *local* calendar fields as the intended day.
export function dateToDueTimestamp(date: Date): Timestamp {
  return Timestamp.fromDate(new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())));
}

// Rebuild a local-midnight Date with the same calendar day, which is what the datepicker displays.
export function dueTimestampToDate(timestamp: Timestamp): Date {
  const utc = Timestamp.toDate(timestamp);
  return new Date(utc.getUTCFullYear(), utc.getUTCMonth(), utc.getUTCDate());
}

// Compares calendar days only: a task due today is not overdue until tomorrow.
export function isOverdue(dueDate: Timestamp | undefined, now: Date = new Date()): boolean {
  if (!dueDate) return false;
  const due = Timestamp.toDate(dueDate);
  const dueDay = Date.UTC(due.getUTCFullYear(), due.getUTCMonth(), due.getUTCDate());
  const today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return dueDay < today;
}
