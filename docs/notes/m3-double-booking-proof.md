# Proof of the double-booking bug

Recorded on 2026-11-07, running `Homestay.Tests/BookingRaceTests.cs` against a real Postgres 16 in Docker, before
the fix. `BookingService.CreateAsync` checked for a free room, then inserted a booking. Each test fires 20 requests
at the same moment.

| Test | Bookings expected | Bookings the naive code accepted |
|---|---|---|
| Last room, same dates (1 room) | 1 | **10** |
| Fill the rooms, same dates (3 rooms) | 3 | **20** |
| Different dates sharing one night (1 room) | 1 | **20** |

## Why it happens

The check and the insert are two separate steps. Every request can run its check before any of them has saved, so
they all see a free room and all insert. Adding more checks in the application can't close that gap.

## The fix

Each booking now also writes one `BookingNights` row per night, and a partial unique index on
`(RoomId, NightDate)` covers every night that isn't cancelled. Postgres enforces this however many requests arrive at
once: the second insert fails with error `23505`, and the API turns that into `409 Conflict`, or tries the next free
room of that type.
