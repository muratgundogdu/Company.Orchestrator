# Excel Capability — Manual Test Guide

## Overview

The Excel capability is backed by **ClosedXML 0.104.x** and supports:

| Handler | Type key | Purpose |
|---|---|---|
| `ExcelReadStepHandler` | `excel.read` | Read sheet → JSON variable |
| `ExcelWriteStepHandler` | `excel.write` | Write rows → `.xlsx` artifact |
| `ExcelWriteCellStepHandler` | `excel.write-cell` | Update one cell → new `.xlsx` artifact |
| `ExcelToCsvStepHandler` | `excel.to-csv` | Export sheet → CSV text + optional `.csv` artifact |

---

## Prerequisites

1. API running: `dotnet run --project src/Company.Orchestrator.Api`
2. Worker running: `dotnet run --project src/Company.Orchestrator.Worker`
3. Swagger UI at: `http://localhost:5000/swagger`

---

## Test 1 — Write Excel from Static Data

### 1a. Create a ProcessDefinition

```http
POST http://localhost:5000/api/process-definitions
Content-Type: application/json

{
  "name": "Excel Write Demo",
  "description": "Creates a sales report xlsx"
}
```

Note the returned `id` (e.g. `abc-123`).

### 1b. Create a ProcessVersion with the workflow definition

```http
POST http://localhost:5000/api/process-definitions/{id}/versions
Content-Type: application/json

{
  "jsonDefinition": "{\"name\":\"Excel Write Demo\",\"steps\":[{\"id\":\"step1\",\"name\":\"Write Sales Data\",\"type\":\"excel.write\",\"config\":{\"outputName\":\"sales.xlsx\",\"sheetName\":\"Sales\",\"includeHeader\":\"true\",\"staticData\":\"[{\\\"Region\\\":\\\"North\\\",\\\"Units\\\":120},{\\\"Region\\\":\\\"South\\\",\\\"Units\\\":85}]\"},\"nextStepId\":null}]}",
  "changeNotes": "Initial version"
}
```

**Tip:** Use the JSON files in `docs/excel-workflows/` as the `jsonDefinition` value (escape the JSON string or use the file contents).

### 1c. Start a ProcessInstance

```http
POST http://localhost:5000/api/process-instances/start
Content-Type: application/json

{
  "processDefinitionId": "{definitionId}",
  "inputData": "{}"
}
```

### 1d. Watch the Worker

The worker picks up the job within a few seconds. Look for:

```
[INF] ExcelCapability: persisted workbook 'sales.xlsx' (1234 bytes) → artifact {guid}
[INF] WorkflowEngine: step 'step1' completed
```

### 1e. Verify the artifact

```http
GET http://localhost:5000/api/artifacts/process-instance/{processInstanceId}
```

Expected response: array with one artifact, `name: "sales.xlsx"`, `contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"`.

### 1f. Download and verify

```http
GET http://localhost:5000/api/artifacts/{artifactId}/download
```

Open the downloaded `.xlsx` in Excel. You should see:
- Sheet tab named **Sales**
- Bold header row: `Region | Units`
- Two data rows

---

## Test 2 — Read Excel and Verify Row Count

Use workflow `02-read-excel.json`. After execution:

1. GET the `ProcessInstance` by ID — check `OutputData` on the `excel.read` step instance.
2. The `employeeRows` variable is a JSON array of 4 employee dictionaries.
3. `employeeRows_count` = `4`.

---

## Test 3 — Write Cell with Variable Interpolation

Use workflow `03-write-cell.json`. Start the process with custom input variables:

```http
POST http://localhost:5000/api/process-instances/start
Content-Type: application/json

{
  "processDefinitionId": "{id}",
  "inputData": "{\"statusValue\":\"APPROVED\",\"approvedBy\":\"Jane Director\",\"approvalDate\":\"2026-06-18\"}"
}
```

After execution, download `approval-final.xlsx`. Expected content:

| A | B |
|---|---|
| Invoice #: | INV-2026-0042 |
| Amount: | $12,750.00 |
| Status: | APPROVED |
| Approved By: | Jane Director |
| Date: | 2026-06-18 |

Each `excel.write-cell` step produces its own artifact — the chain produces three intermediate
artifacts (`approval-with-status.xlsx`, `approval-with-approver.xlsx`, `approval-final.xlsx`).
All four are queryable via `GET /api/artifacts/process-instance/{id}`.

---

## Test 4 — Excel to CSV Export

Use workflow `04-excel-to-csv.json`. After execution verify:

1. Two artifacts exist: `products.xlsx` and `products-export.csv`.
2. Download `products-export.csv`. Open in a text editor. Expected:

```csv
SKU,Name,Category,Price,Stock
P001,Blue Widget,Hardware,29.99,150
P002,Red Widget,Hardware,34.99,89
P003,Basic Service,Service,99.99,999
P004,Premium Plan,Service,199.99,999
P005,"Green, Large Widget",Hardware,49.99,42
```

Note: `Green, Large Widget` is correctly quoted because it contains a comma.

3. The `excel.read` step that reads back the CSV produces `csvRows_count = 5`.

---

## Cell Address Reference

ClosedXML accepts standard Excel cell addresses:

| Address | Column | Row |
|---|---|---|
| `A1` | 1st | 1st |
| `B3` | 2nd | 3rd |
| `Z10` | 26th | 10th |
| `AA1` | 27th | 1st |
| `AB5` | 28th | 5th |

---

## Config Reference

### `excel.write`

| Key | Required | Default | Description |
|---|---|---|---|
| `outputName` | ✓ | — | Output `.xlsx` file name |
| `sheetName` | | `Sheet1` | Worksheet name |
| `dataVariable` | * | — | Context variable with JSON row array |
| `staticData` | * | — | Inline JSON row array |
| `inputArtifact` | | — | Existing workbook to write into |
| `includeHeader` | | `true` | Write bold header row |

*Either `dataVariable` or `staticData` must be provided.

### `excel.read`

| Key | Required | Default | Description |
|---|---|---|---|
| `artifactName` | ✓ | — | Context artifact name (xlsx or csv) |
| `sheetName` | | `Sheet1` | Sheet to read |
| `hasHeaderRow` | | `true` | First row treated as headers |
| `outputVariable` | | `excelRows` | Variable name for JSON array |

### `excel.write-cell`

| Key | Required | Default | Description |
|---|---|---|---|
| `artifactName` | ✓ | — | Source workbook artifact in context |
| `sheetName` | ✓ | — | Worksheet name |
| `cellAddress` | ✓ | — | Excel address, e.g. `B3` |
| `value` | ✓ | — | Value to write (supports `{{variable}}`) |
| `outputArtifactName` | | `{name}-updated.xlsx` | New artifact name |

### `excel.to-csv`

| Key | Required | Default | Description |
|---|---|---|---|
| `artifactName` | ✓ | — | Source workbook artifact in context |
| `sheetName` | | `Sheet1` | Sheet to export |
| `outputVariable` | | `csvText` | Variable for the CSV text |
| `saveAsArtifact` | | `false` | Save CSV as a file artifact |
| `outputArtifactName` | | `{name}.csv` | Name for the CSV artifact |

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Sheet 'X' not found in workbook` | Wrong `sheetName` in config | Use `GetSheetNamesAsync` (planned endpoint) or check source file |
| `artifact 'X' not found in context` | Artifact name mismatch | Ensure the `outputName` of the write step matches `artifactName` of the read step |
| Download returns empty `.xlsx` | Empty `staticData` / `dataVariable` resolved to null | Verify the JSON is valid and the variable exists in context |
| CSV last column has extra data | Multi-line cell values | Values with newlines are quoted per RFC 4180 — correct behaviour |
| `ClosedXML` exception on open | File is not a valid `.xlsx` | Ensure the artifact was produced by `excel.write`, not a non-Excel file |
