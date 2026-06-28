# Phase 4 Validation — Mail Attachment Processing

Step-by-step guide to validate the complete mail → attachment → Excel → folder pipeline using Swagger.

---

## Prerequisites

- API running at `http://localhost:5000` (Swagger UI at `http://localhost:5000`)
- Gmail configured in `appsettings.json` under `Mail:Imap` and `Mail:Smtp`
- An email in Gmail INBOX with:
  - Subject containing **RAPOR TEST**
  - An Excel (`.xlsx`) attachment
  - A sheet named **Data** inside the workbook
- `C:\Temp\AlterOneOutput\` directory exists (the workflow creates it automatically if the folder capability has write access)

---

## Step 1 — TCP Reachability Check

Before testing mail, confirm the network can reach Gmail IMAP.

**Endpoint:** `GET /api/diagnostics/imap-tcp`

Expected response (`200 OK`):
```json
{
  "host": "imap.gmail.com",
  "port": 993,
  "result": "TCP connection successful",
  "success": true,
  "probed": "2026-06-19T..."
}
```

If you get `503`, IMAP port 993 is blocked on your network. Check firewall/proxy settings before continuing.

---

## Step 2 — Register the Workflow Definition

**Endpoint:** `POST /api/process-definitions`

Paste the contents of `docs/mail-workflows/05-mail-attachment-to-folder.json` into the request body.

```json
{
  "name": "mail-attachment-to-folder",
  "description": "Phase 4 validation workflow...",
  "version": "1",
  "steps": [ ... ]
}
```

Save the returned `id` — this is your `processDefinitionId`.

**Expected response (`201 Created`):**
```json
{
  "id": "<processDefinitionId>",
  "name": "mail-attachment-to-folder",
  ...
}
```

---

## Step 3 — Start a Process Instance

**Endpoint:** `POST /api/process-instances`

```json
{
  "processDefinitionId": "<processDefinitionId>",
  "correlationId": "phase4-test-001",
  "variables": {}
}
```

Save the returned `id` — this is your `processInstanceId`.

---

## Step 4 — Trigger a Job

**Endpoint:** `POST /api/jobs`

```json
{
  "processInstanceId": "<processInstanceId>"
}
```

Save the returned `id` — this is your `jobId`.

---

## Step 5 — Monitor Job Execution

**Endpoint:** `GET /api/jobs/{jobId}`

Poll until `status` is `Completed` or `Failed`.

**Expected log sequence (visible in the API/Worker console):**

```
MailReadAttachmentsStepHandler: searching 'INBOX' for messages with attachments (maxCount=1)
MailCapability IMAP: Connecting to imap.gmail.com:993 ...
MailCapability IMAP: Connected successfully to imap.gmail.com
MailCapability IMAP: Authenticating as '...'
MailCapability IMAP: Authenticated successfully as '...'
MailCapability IMAP: search returned N UID(s)
MailCapability IMAP: applying MaxCount=1
MailCapability IMAP: fetching summaries for 1 message(s)...

MailReadAttachmentsStepHandler: message — Subject='RAPOR TEST ...', From='sender@example.com', Attachments=[report.xlsx]
MailReadAttachmentsStepHandler: artifact — Id=<guid>, Name='rapor-test_report.xlsx', ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', SizeBytes=12345
MailReadAttachmentsStepHandler: downloaded 1 attachment(s) from 1 message(s)

ExcelReadStepHandler: reading sheet 'Data' from artifact 'rapor-test_report.xlsx'
  → Read N rows from sheet 'Data'

FolderWriteFile: writing artifact 'rapor-test_report.xlsx' (12345 bytes) → 'C:\Temp\AlterOneOutput\rapor-test_report.xlsx' (overwrite=True)
  → Wrote 12345 bytes → 'C:\Temp\AlterOneOutput\rapor-test_report.xlsx'
```

---

## A — Verify mail.read-attachments

**Endpoint:** `GET /api/jobs/{jobId}`

In the job response, check `stepLogs` or `outputData` for the `download-attachment` step.

You should see:
- `Subject`: the email subject containing "RAPOR TEST"
- `From`: the sender email address
- `Attachments`: the Excel filename(s)
- Artifact count: `1`

---

## B — Verify Artifact Creation

**Endpoint:** `GET /api/artifacts/process-instance/{processInstanceId}`

Expected response:
```json
[
  {
    "id": "<artifact-guid>",
    "name": "rapor-test_<originalFileName>.xlsx",
    "contentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "sizeBytes": 12345,
    "storagePath": "...",
    "downloadUrl": "http://localhost:5000/api/artifacts/<guid>/download"
  }
]
```

Confirm:
- `id` is a non-empty GUID
- `name` starts with `rapor-test_`
- `contentType` is `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `sizeBytes` > 0

**To download the artifact and inspect it:**

`GET /api/artifacts/{id}/download`

The browser (or Swagger) will return the raw `.xlsx` file.

---

## C — Verify Excel Processing

In the job step output for `read-excel`, confirm:

- `excelRows_count` > 0 (the number of data rows in the "Data" sheet)
- `excelRows` contains a JSON array of row objects, e.g.:
  ```json
  [{"Column1": "Value1", "Column2": "Value2"}, ...]
  ```

If the step fails with `"artifact 'rapor-test_...' not found in context"`, it means the attachment filename differs from what was stored. Check the artifact name returned in Step B and verify it matches `{{mailArtifacts_0}}` (the first element of the `mailArtifacts` variable set by the previous step).

If the step fails with a sheet-not-found error, verify the Excel file has a sheet named exactly **Data** (case-sensitive).

---

## D — Verify Folder Output

After the job completes with `status: Completed`:

1. Open `C:\Temp\AlterOneOutput\` in Windows Explorer
2. Confirm the file `rapor-test_<originalFileName>.xlsx` exists
3. Open it to verify it is a valid, uncorrupted Excel workbook

The `write-to-folder` step log should show:
```
Wrote <N> bytes → 'C:\Temp\AlterOneOutput\rapor-test_<originalFileName>.xlsx'
```

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `TCP connection failed` on `/api/diagnostics/imap-tcp` | Port 993 blocked | Check firewall, try from a different network |
| `IMAP receive timed out` | Slow network or wrong SSL option | Increase `Mail:Imap:TimeoutSeconds` in appsettings |
| `downloaded 0 attachment(s)` | No email matching subject + hasAttachments | Send a test email to the Gmail account with subject "RAPOR TEST" and an Excel attachment; set `unreadOnly: false` |
| `artifact '...' not found in context` | `excel.read` referencing wrong artifact name | Check `mailArtifacts_0` variable in job output; verify it matches the artifact name in `/api/artifacts/process-instance/{id}` |
| `Sheet 'Data' not found` | Excel workbook has a different sheet name | Open the attachment and check sheet names; update `sheetName` in the workflow definition |
| `Access denied` on `folder.write-file` | Process does not have write access to `C:\Temp\AlterOneOutput\` | Create the folder manually or run the API/Worker as an account with write access |

---

## Workflow Definition Reference

File: `docs/mail-workflows/05-mail-attachment-to-folder.json`

| Step ID | Type | Key config |
|---------|------|-----------|
| `download-attachment` | `mail.read-attachments` | folder=INBOX, subjectContains="RAPOR TEST", maxCount=1, artifactPrefix=rapor-test |
| `read-excel` | `excel.read` | artifactName=`{{mailArtifacts_0}}`, sheetName=Data |
| `write-to-folder` | `folder.write-file` | artifactName=`{{mailArtifacts_0}}`, destinationPath=`C:\Temp\AlterOneOutput\{{mailArtifacts_0}}`, overwrite=true |

**Variable chaining:**

```
mail.read-attachments
  → sets mailArtifacts      = '["rapor-test_report.xlsx"]'  (JSON array)
  → sets mailArtifacts_0    = 'rapor-test_report.xlsx'      (first artifact name, plain string)
  → sets mailArtifacts_count = 1

excel.read (uses {{mailArtifacts_0}})
  → sets excelRows           = '[{"Col1":"Val1",...},...]'   (JSON array of row objects)
  → sets excelRows_count     = N

folder.write-file (uses {{mailArtifacts_0}})
  → sets writtenPath         = 'C:\Temp\AlterOneOutput\rapor-test_report.xlsx'
  → sets writtenSizeBytes    = N
```
