# SQLite retention strategy

## Goals

- Prevent unbounded growth of incidents and logs.
- Enforce all retention at the **persistence layer** (SQLite); no unbounded in-memory state.

## Config (Retention section)

| Setting                  | Meaning                                      | Example |
|--------------------------|----------------------------------------------|---------|
| MaxActiveIncidents       | Max open/investigating incidents at once     | 10      |
| MaxTotalIncidents        | Max total incidents retained                 | 100     |
| MaxLogEntries            | Max log rows in the database                 | 5000    |
| AutoResolveIdleMinutes   | Idle time after which an incident is resolved | 15    |

Set to `0` to disable a limit (e.g. in development).

## Enforcement

- **Active incident cap**: Before creating a new incident, if the number of active (non-resolved) incidents is at or above `MaxActiveIncidents`, the oldest resolved incidents are deleted until the count is below the cap. Then the new incident is added.
- **Total incident cap**: A background job (e.g. every 2 minutes) counts total incidents. If over `MaxTotalIncidents`, it deletes the **oldest resolved** incidents (by `EndTime` / `UpdatedAt`) until total is at or below the cap. **Active incidents are never deleted** by retention.
- **Log cap**: After each batch of logs is written, total log count is checked. If over `MaxLogEntries`, the **oldest** logs (by `Timestamp`) are deleted until the count is at or below the cap.
- **Auto-resolution**: The same background job marks incidents as Resolved (and sets `EndTime`) when they have had no activity for `AutoResolveIdleMinutes` (using `UpdatedAt` as proxy for last activity).

## Purge order

- Incidents: always **oldest resolved first**; active incidents are never purged by retention.
- Logs: **oldest by timestamp** first.
