# Mentora Web Creator

Local development frontend for the creator platform.

## Run

1. Install dependencies:

```bash
npm install
```

2. Copy the env file if you want to override the API host:

```bash
cp .env.example .env.local
```

3. Start the dev server:

```bash
npm run dev
```

By default the frontend talks to:

```text
https://neurokey.serenityutils.club
```

So the local site can edit courses against the VPS backend.
