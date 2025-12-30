# Accounting Engine Requirements

The goal of this application is to provide a robust web API interface for a double-entry accounting engine with integrated inventory management. It serves as a backend platform for other applications to manage multi-company and multi-currency financial data.

## Core Principles

1.  **Double-Entry**: Every transaction must have a net sum of zero (Sum of Amounts = 0). Positive amounts represent Debits, Negative amounts represent Credits.
2.  **Multi-Tenancy**: Data is segregated by "Company" or "Tenant".
3.  **Multi-Currency (Parallel Ledgers)**:
    - The engine does not store global exchange rates.
    - All reporting currencies must be calculated by the client and sent as parallel journal entries at the time of posting.
    - Forex Gain/Loss entries are explicitly created as needed by the client logic.
4.  **Immutability**: Posted accounting entries are rarely deleted; they are reversed to maintain audit trails.

## Entities

### 1. Commercial Entities (The "What")

These entities capture the business activity.

- **Contact**: `(id, name, type [Customer/Vendor], tax_id, currency, receivables_account_id, payables_account_id)`
  - _Purpose_: Stores defaults for accounting behavior per client/supplier.
- **Product**: `(id, name, sku, type [Service/Goods], sales_account_id, expense_account_id, inventory_account_id)`
  - _Purpose_: Maps items to specific General Ledger (GL) accounts.
- **Invoice**: `(id, type [Sales/Purchase], contact_id, ref, date, due_date, currency, exchange_rate, status [Draft, Authorised, Paid, Voided], total_amount, balance_due)`
  - _Note_: Separate "paid" boolean is insufficient; track `balance_due` to allow partial payments.
- **InvoiceLine**: `(id, invoice_id, product_id, description, quantity, unit_price, discount_amount, tax_amount, subtotal, total)`
- **Payment**: `(id, date, contact_id, bank_account_id, amount, currency, exchange_rate, ref, type [Inbound/Outbound])`
  - _Purpose_: Represents cash flow. Independent of Invoices until "Allocated".
- **Allocation**: `(id, payment_id, invoice_id, amount)`
  - _Purpose_: Matches payments to invoices to clear balances.

### 2. Inventory Entities (The "Stuff")

- **Warehouse**: `(id, name, location)`
- **StockMovement**: `(id, date, ref, type [In/Out/Transfer/Adjustment], contact_id, warehouse_id, status)`
  - _Crucial_: Explicit discrimination between "In" (Purchase) and "Out" (Sale) logic.
- **StockMovementLine**: `(id, movement_id, product_id, quantity, unit_cost, total_cost)`

### 3. Accounting Entities (The "Scoreboard")

The General Ledger (GL) is the source of truth.

- **Account**: `(id, code, name, type [Asset, Liability, Equity, Revenue, Expense], sub_type, parent_id)`
  - _Improvement_: `code` allows for hierarchical sorting. `parent_id` allows for tree-view reporting.
- **Journal (Header)**: `(id, date, source_type [Invoice, Payment, Stock], source_id, reference, narration, status)`
- **JournalLine (Detail)**: `(id, journal_id, account_id, description, amount, currency)`
  - _Constraint_: `Sum(Amount)` must equal `0` for every unique `(journal_id, currency)` tuple.
  - _Note_: `amount` is signed. typically Debit is Positive (+), Credit is Negative (-).
  - _Multiple Currencies_: To support multiple reporting currencies, distinct lines with the calculated amounts for each currency are stored.

## Operations & Accounting Impact

### Invoicing

- **Post Sales Invoice**:
  - +Receivable Amount (Asset)
  - -Revenue Amount (Income)
  - -Tax Payable Amount (Liability)
- **Post Purchase Invoice**:
  - +Expense/Inventory Amount (Expense/Asset)
  - +Input Tax Amount (Asset)
  - -Accounts Payable Amount (Liability)

### Payments

- **Receive Payment**:
  - +Bank Amount
  - -Receivables Amount

### Inventory

- **Ship Goods**:
  - +COGS
  - -Inventory
- **Receive Goods**:
  - +Inventory
  - -GRNI/AP

### Lifecycle

- **Voiding**: Should create a _Reversal Journal_ (swapped Debits/Credits) rather than deleting the original record.
- **Locking**: Implementing a "Period Lock Date" involves checking `Invoice.date` > `Company.lock_date` before allowing Create/Update/Delete.

## Reporting Interface

Reports are efficient aggregations of `JournalLine` data filtered by currency.

- **Trial Balance**: Sum `amount` group by `account_id`. Must equal 0.
- **General Ledger Detail**: Line-by-line view of a specific Account's activity.
- **Aged Receivables/Payables**: Derived from `Invoice` status and `Allocation` data (not just GL, for better matching).
- **Multi-Currency**: Since ledgers are parallel, simply filter by `currency` to get the report in that currency. No on-the-fly conversion is done.
