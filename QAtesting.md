# QA Testing — Power BI Modeling MCP Server (Round 2)

**Date:** 2026-02-14  
**Model:** Adventure Works DW 2020  
**Transport:** HTTP/Streamable-HTTP on port 5100  
**Tables in model:** Currency, Currency Rate, Customer, Date, Product, Reseller, Sales, Sales Order, Sales Reason, Sales Reason Bridge, Sales Territory

---

## Test Summary

| Category | Tests | Passed | New (Round 2) |
|----------|-------|--------|---------------|
| Connection | 6 | 6 | 1 (A05 disconnect feedback) |
| Model | 2 | 2 | 0 |
| Tables | 4 | 4 | 2 (C03, C04 validation) |
| Columns | 5 | 5 | 3 (D03-D05 validation) |
| Measures | 8 | 8 | 3 (E03, E04, E06 validation) |
| Relationships | 3 | 3 | 1 (F03 Enum.TryParse) |
| DAX Queries | 10 | 10 | 0 |
| MCP Annotations | 5 | 5 | 5 (all new) |
| Security | 5 | 5 | 5 (all new) |
| Edge Cases | 4 | 4 | 0 |
| **Total** | **52** | **52** | **20** |

---

## Test Results

### A. Connection Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| A01 | Discover PBI Desktop instances (no fileName) | ✅ | Found Adventure Works DW 2020 |
| A02 | Connect to Adventure Works DW 2020 | ✅ | Returns 12-char connection ID `8a41ca0acfd7` |
| A03 | List connections (verify password redaction) | ✅ | Shows Adventure Works, Password=*** |
| A04 | Disconnect with valid ID | ✅ | Returns "Disconnected" + post-disconnect query rejected |
| A05 | Disconnect with invalid ID | ✅ | **NEW:** Returns "not found — it may have already been disconnected" |
| A06 | Reconnect after disconnect | ✅ | New ID `6add6e3907d9` works |

### B. Model Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| B01 | Get model info | ✅ | Tables, relationships, compat level shown |
| B02 | Get model stats via DAX | ✅ | Total Tables/Columns/Measures/Relationships |

### C. Table Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| C01 | List all tables | ✅ | 11 tables with markdown formatting |
| C02 | Get Currency table details | ✅ | CurrencyKey, Currency Code, Currency, Currency Format |
| C03 | Create table with empty name | ✅ | **NEW:** "Table name cannot be empty" |
| C04 | Rename table with empty name | ✅ | **NEW:** "New table name cannot be empty" |

### D. Column Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| D01 | List columns in Currency | ✅ | Shows CurrencyKey + columns |
| D02 | Get column details | ✅ | Full metadata with Data Type, SortBy, etc. |
| D03 | Create column with invalid data type | ✅ | **NEW:** "Invalid data type 'InvalidType'. Valid values: ..." |
| D04 | Create column with empty name | ✅ | **NEW:** "Column name cannot be empty" |
| D05 | Rename column with empty name | ✅ | **NEW:** "New column name cannot be empty" |

### E. Measure Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| E01 | List all measures | ✅ | Returns measures from model |
| E02 | Create measure | ✅ | "QA Test Measure" created in Currency |
| E03 | Create measure with empty name | ✅ | **NEW:** "Measure name cannot be empty" |
| E04 | Create measure with >256 chars name | ✅ | **NEW:** "Measure name exceeds 256 character limit" |
| E05 | Update measure expression | ✅ | Expression updated |
| E06 | Rename with empty new name | ✅ | **NEW:** "New measure name cannot be empty" |
| E07 | Rename measure | ✅ | Renamed to "QA Renamed Measure" |
| E08 | Delete measure | ✅ | Deleted and verified |

### F. Relationship Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| F01 | List relationships | ✅ | All relationships with markdown escaping |
| F02 | Find relationships for Sales | ✅ | Shows related tables with arrows |
| F03 | Create relationship with invalid crossFilter | ✅ | **NEW:** "Invalid cross-filter value. Valid values: ..." |

### G. DAX Query Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| G01 | Execute basic DAX query | ✅ | TOPN(5, 'Currency') returns 5 rows |
| G02 | Preview table data | ✅ | Currency preview with 3 rows |
| G03 | Get distinct values | ✅ | Distinct currencies returned |
| G04 | Evaluate measure expression | ✅ | COUNTROWS result correct |
| G05 | DMV tables info | ✅ | Storage info returned |
| G06 | DMV measures info | ✅ | Measure definitions returned |
| G07 | DMV relationships info | ✅ | Relationship metadata returned |
| G08 | DAX injection (table name with quotes) | ✅ | Injection blocked with safe error |
| G09 | Invalid DAX (no EVALUATE) | ✅ | Error with EVALUATE hint |
| G10 | Markdown escaping in results | ✅ | Cell values properly escaped |

### H. MCP Annotation Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| H01 | tmdl_export ReadOnly=false | ✅ | **NEW:** readOnlyHint=False (was True) |
| H02 | Delete tools Destructive=true | ✅ | **NEW:** All 5 delete/destructive tools annotated |
| H03 | Connect tools Idempotent=false | ✅ | **NEW:** All 3 connect tools show idempotent=False |
| H04 | 12-char connection IDs | ✅ | **NEW:** ID length=12 (was 8) |
| H05 | Server version from assembly | ✅ | **NEW:** version=1.0.0 from assembly, not hardcoded |

### I. Security Tests

| # | Test | Result | Notes |
|---|------|--------|-------|
| I01 | tmdl_read_file with .exe extension | ✅ | **NEW:** "Access to system directory 'C:\WINDOWS' is not allowed" |
| I01b | tmdl_read_file with .txt (non-system) | ✅ | **NEW:** "Only .tmdl and .json files can be read. Got: '.txt'" |
| I02 | tmdl_read_file with system path | ✅ | **NEW:** "Access to system directory 'C:\WINDOWS' is not allowed" |
| I03 | pbip_discover with C:\Windows | ✅ | **NEW:** "Access to system directory 'C:\WINDOWS' is not allowed" |
| I05 | Disconnect reports not-found correctly | ✅ | **NEW:** "not found — it may have already been disconnected" |

### J. Edge Cases

| # | Test | Result | Notes |
|---|------|--------|-------|
| J01 | Invalid connectionId | ✅ | "Connection 'invalid12345' not found" |
| J02 | Table not found | ✅ | "Table 'NonExistentTable' not found" |
| J03 | topN=0 boundary | ✅ | Clamped to 1, returned 1 row |
| J04 | Empty table name in preview | ✅ | Graceful error |

---

## Build Verification

```
dotnet build src/PowerBiMcpServer/PowerBiMcpServer.csproj
Build succeeded. 0 Warning(s), 0 Error(s)
```

All 52 tests passed against live Power BI Desktop (Adventure Works DW 2020).

