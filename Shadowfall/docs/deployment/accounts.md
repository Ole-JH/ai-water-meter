# Accounts & passwords

Players log in to an **account** (account name + password, optionally an email address). An account holds up to
**10 characters** (heroes); after logging in, players pick one on the character select screen or create a new one.

| | Rules |
| --- | --- |
| Account names, character names | 3–16 letters, digits or `_`, starting with a letter. Unique, ignoring case. Account names and character names are separate: an account and a character may share a name |
| Passwords | 6–128 characters |
| Email | Optional, unique per account. Only used for password reset codes |
| Characters per account | 10. Deleting a hero needs the account password |

Logging in to an account from a second place logs out the first. Changing or resetting the password logs out every other
place the account was logged in. Connections that never log in are closed after 15 minutes.

## Where accounts are stored

| How the server runs | Storage | Where |
| --- | --- | --- |
| Docker Compose (`task up`) | PostgreSQL 17 (`DATABASE_URL` is set) | The `postgres` container, data in the `shadowfall-postgres-data` volume |
| `task server:dev`, `npm start`, the smoke test | JSON files (no `DATABASE_URL`) | `DATA_DIR` (`server/data`): `accounts/<name>.json`, `characters/<name>.json`, `resets.json`, `account-events.log`; deleted heroes are moved to `deleted-characters/` |

The server logs which one it uses at startup (*"Accounts are stored in PostgreSQL"*) and exports it as
`shadowfall_storage_info{kind="postgres"|"files"}`. It creates and migrates the tables itself (`schema_migrations` records the
version; version 1 has `accounts`, `characters`, `password_resets`, `account_events` and `meta`; version 2 adds `guilds`, one JSON
document per guild. Without a database, guilds are in `guilds.json` in the data folder).

The world map (`world.json`) stays in `server/data` either way.

!!! danger "Keep the database volume"
    `task down` (`docker compose down`) keeps the `shadowfall-postgres-data` volume. `docker compose down -v` deletes it,
    and with it every account and character.

## Upgrading from character files

Before accounts, each character was a file `server/data/characters/<name>.json` with its own password. On the first start
with the database, the server imports them once:

- every old character becomes an **account with the same name and password**, holding that one character with all its progress
  (an `"admin": true` flag is kept);
- the files are **kept** in `server/data/characters` as a backup (they are not read again);
- the log says *"Imported N account(s) and N character(s) ... into PostgreSQL"*.

Players log in as before, with their character name as the account name. The first time, they are shown a
**recovery code** (see below). They can then create more heroes on the same account.

With file storage (`task server:dev`) the same import runs, but the old files are converted in place: the password moves to
`accounts/<name>.json`.

??? note "Running the import again"
    The import runs only once per database (`meta` row `files_imported`). To import files added later, delete that row
    (`DELETE FROM meta WHERE key = 'files_imported';` in `task db:psql`) and restart the game server. Accounts and characters whose
    names already exist are skipped.

## Resetting a password

There are three ways, all on the login screen under **Forgot password?**:

| Way | Who | How |
| --- | --- | --- |
| **Recovery code** | The player | **Forgot password?** → **I have a code**: account name, recovery code, new password. The code then stops working and a new one is shown |
| **Email** | The player, if the account has an email and the server can send mail | **Forgot password?** → account name or email → **Email Me a Code**. The email contains a reset code (valid 30 minutes) and, with `PUBLIC_URL` set, a link that opens the reset form already filled in |
| **Admin reset code** | An admin | `/a resetpw <account or character>` in game chat, or `task account:reset -- <account or character>` on the server. The code is valid for 24 hours. Give it to the player; they enter it under **I have a code** |

All reset codes work **once**. A successful reset logs the player in.

### Recovery codes

Every account has a recovery code like `K7QM-2XRP-H9TA-WC4N` (16 characters, without `0`/`O` and `1`/`I`/`L`; case, spaces and
dashes don't matter when typing it). It is shown only once: when the account is created, at the first login of an imported
account, after it was used, or when the player asks for a new one (++esc++ → **Account** → **Get a New Recovery Code**, which
makes the old one invalid). Players should keep it in a password manager.

### Reset emails (optional)

Email resets are off until `SMTP_URL` is set. Put these in `server/.env` and run `task up`:

| Variable | Example | Meaning |
| --- | --- | --- |
| `SMTP_URL` | `smtps://you%40gmail.com:apppassword@smtp.gmail.com:465` | SMTP server, as a URL (passed to nodemailer) |
| `MAIL_FROM` | `Shadowfall <you@gmail.com>` | Sender address (default `Shadowfall <no-reply@localhost>`) |
| `PUBLIC_URL` | `https://play.example.com` | The game's address, for the link in the email (`<PUBLIC_URL>/?reset=CODE&user=NAME`). Without it, the email only contains the code |

!!! example "Gmail"
    1. Turn on 2-step verification for the Google account, then create an **app password** at
       <https://myaccount.google.com/apppasswords>.
    2. In `server/.env`:

        ```bash
        SMTP_URL=smtps://shadowfall.game%40gmail.com:abcdefghijklmnop@smtp.gmail.com:465
        MAIL_FROM=Shadowfall <shadowfall.game@gmail.com>
        PUBLIC_URL=https://play.example.com
        ```

    The user name in the URL is the full address with `@` written as **`%40`**. Write the app password without its spaces.
    Any other special character in the user name or password must be URL-encoded too (`:` → `%3A`, `/` → `%2F`, `#` → `%23`).

The login screen asks the server whether it can send mail. Without `SMTP_URL`, **Email Me a Code** answers *"This server can't
send emails"* and players can still store an email for later. To protect players' privacy, the answer to a reset request is the
same whether or not the account or email exists. Failed sends are logged as *"reset email failed"*.

### Changing account details in game

++esc++ → **Account**: change the password, set or remove the email address, or get a new recovery code. Each change needs the
current password.

## Rate limits

| What | Limit | Then |
| --- | --- | --- |
| Wrong passwords or codes for one account | 5 in 10 minutes | The account is locked for 2 minutes |
| Wrong passwords or codes from one address | 25 in 15 minutes | The address is blocked for 15 minutes |
| New accounts from one address | 10 per hour | Refused until the hour is over |
| Reset emails requested from one address | 5 per hour | Refused until the hour is over |

Players see *"Too many wrong attempts. Try again in ..."*. The counters live in the server's memory, so a restart
(`task restart`) clears them. Refused attempts are counted as `shadowfall_logins_total{result="locked"}`, and many failures fire
the `ShadowfallLoginAttack` alert ([Monitoring](monitoring.md#alerts)).

!!! warning "Behind a reverse proxy, set `TRUST_PROXY=1`"
    Otherwise every player appears to come from the proxy's address and shares one set of address limits. With
    `TRUST_PROXY=1` (in `server/.env`) the server takes the first address in `X-Forwarded-For`, so the proxy must set that header
    and replace any value sent by the client (see [HTTPS & reverse proxy](reverse-proxy.md#client-addresses)). Don't set it when
    players connect to the game port directly: they could fake their address.

## Admin command line

`server/admin-cli.js` works while the server runs. Task wraps it:

| Command | What it does |
| --- | --- |
| `task account:reset -- Alice` | One-time reset code for the account `Alice`, or for the account that owns the character `Alice` (24 hours) |
| `task account:admin -- Alice on` / `off` | Give or take admin rights (takes effect at the player's next login) |
| `task accounts` / `task accounts -- ali` | List accounts (optionally filtered) with email, characters and levels, and last login |

Without Task: `docker compose exec shadowfall node admin-cli.js reset-code Alice` (in `server/`). Locally, with file storage:
`cd server && node admin-cli.js accounts`.

## Database

### Password

The database user is `shadowfall`, the database `shadowfall`. Its password comes from `POSTGRES_PASSWORD` in `server/.env`
(default `shadowfall`; it isn't reachable from outside Docker's network, but set a real one). Use letters and digits only: it is
also put into connection URLs.

!!! warning "`POSTGRES_PASSWORD` only applies when the database is first created"
    Postgres stores the password in its data volume on the first start. Changing `POSTGRES_PASSWORD` later doesn't change it;
    the game server then fails with *"password authentication failed"*. To change it on a running installation:

    ```bash
    task db:psql
    ```
    ```sql
    ALTER ROLE shadowfall PASSWORD 'NewPassword123';
    ```

    then set `POSTGRES_PASSWORD=NewPassword123` in `server/.env` and run `task up` (it recreates the game server and
    postgres-exporter with the new password).

### Backups and restore

`task backup` writes two files into `backups/`:

| File | Contents |
| --- | --- |
| `shadowfall-db-<stamp>.sql.gz` | `pg_dump` of the database: accounts, characters, reset codes, account events (only if the `postgres` container is running) |
| `shadowfall-data-<stamp>.tar.gz` | `server/data`: the world map and the old character files |

Restore the database with:

```bash
task db:restore -- backups/shadowfall-db-20261001-120000.sql.gz
```

It asks for confirmation, stops the game server, replaces every account and character with the backup, and starts it again.

### SQL

`task db:psql` opens a SQL prompt (`\q` quits). Useful queries:

```sql
-- the latest account events (logins, failed logins, resets, password changes, characters created/deleted)
SELECT at, username, event, ip FROM account_events ORDER BY at DESC LIMIT 50;

-- failed logins per address in the last day
SELECT ip, count(*) FROM account_events
WHERE event IN ('login_failed', 'reset_failed') AND at > now() - interval '1 day'
GROUP BY ip ORDER BY count DESC;

-- an account and its characters
SELECT a.username, a.email, a.last_login, c.name, c.look, c.level, c.last_played
FROM accounts a LEFT JOIN characters c ON c.account_id = a.id AND c.deleted_at IS NULL
WHERE lower(a.username) = lower('Alice');

-- the 20 highest-level characters
SELECT name, look, level, last_played FROM characters WHERE deleted_at IS NULL ORDER BY level DESC LIMIT 20;

-- undo a character deletion (deleted heroes are kept; fails if someone took the name since)
UPDATE characters SET deleted_at = NULL WHERE lower(name) = lower('Fernling') AND deleted_at IS NOT NULL;
```

Events are `login`, `login_failed`, `register`, `reset_recovery`, `reset_code`, `reset_failed`, `reset_email_sent`,
`password_changed`, `email_set`, `email_removed`, `recovery_code_changed`, `character_created:<name>`,
`character_deleted:<name>` and `admin_reset_code:<admin>`.

The game server keeps a logged-in account in memory, so changes made with SQL (or `task account:admin`) apply to that player at
their next login.

## Security notes

- Passwords and recovery codes are stored as salted **scrypt** hashes, never in plain text. Checks are constant-time.
- Reset codes from emails and admins are stored as **SHA-256** hashes, work once and expire (30 minutes, 24 hours).
- **Resume tokens** let the game rejoin by itself after a server restart (every deploy), a dropped connection or a reload
  into a new build. One is handed out on entering the world: random, stored as a SHA-256 hash in the same table as reset
  codes (kind `resume:<session version>`), single use, valid 12 hours, and void as soon as the password changes. It is
  never accepted as a reset code (nor a reset code as one). The browser keeps it, but only uses it within 10 minutes of
  having last been in the world, and forgets it on logging out or going back to character select. Wrong tokens count
  against the same rate limits as wrong passwords.
- Wrong passwords and codes are rate-limited per account and per address (above).
- A reset request doesn't reveal whether an account or email exists. A login with a character name instead of an account
  name says so, to help players who upgraded.
- Passwords travel inside the WebSocket connection: use [HTTPS](reverse-proxy.md) for anything public, so they are encrypted.
