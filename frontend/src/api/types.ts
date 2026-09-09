export type Role = 'Admin' | 'Muhasebe' | 'Personel'

export type SourceSystem = 'PMS' | 'POS' | 'ERP'

export type RuleType = 'FixedPercentage' | 'TieredRate' | 'FixedAmountPerTransaction'

export type LineType = 'Sale' | 'Refund' | 'Excluded' | 'RuleSubtotal' | 'Total' | 'Tier'

export interface Employee {
  id: number
  employeeNo: string
  fullName: string
  department: string
  hotelCode: string
  hireDate: string
  terminationDate: string | null
}

export interface Rule {
  id: number
  name: string
  ruleType: RuleType
  parametersJson: string
  productCategory: string | null
  productCode: string | null
  sourceSystem: SourceSystem | null
  departmentId: number | null
  departmentName: string | null
  priority: number
  validFrom: string
  validTo: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export interface RuleInput {
  name: string
  ruleType: RuleType
  parametersJson: string
  productCategory: string | null
  productCode: string | null
  sourceSystem: SourceSystem | null
  departmentId: number | null
  priority: number
  validFrom: string
  validTo: string | null
  isActive: boolean
}

export interface RuleOptions {
  types: { type: RuleType; label: string; parameterHint: string }[]
  categories: string[]
  departments: { id: number; name: string }[]
  sources: SourceSystem[]
}

export interface SaleInfo {
  sourceSystem: SourceSystem
  externalDocumentNo: string
  transactionDate: string
  productCode: string
  productName: string
  productCategory: string
  amount: number
  isRefund: boolean
}

export interface CalculationLine {
  sequence: number
  lineType: LineType
  saleRecordId: number | null
  ruleId: number | null
  ruleName: string | null
  baseAmount: number
  rate: number | null
  amount: number
  description: string
  sale: SaleInfo | null
}

export interface RuleSummary {
  ruleId: number
  ruleName: string
  ruleType: RuleType
  transactionCount: number
  baseAmount: number
  amount: number
}

export interface CommissionResult {
  calculationId: number
  employeeNo: string
  fullName: string
  department: string
  hotelCode: string
  year: number
  month: number
  grossSales: number
  refundTotal: number
  netSales: number
  totalCommission: number
  calculatedAt: string
  calculatedBy: string
  isFinal: boolean
  periodClosed: boolean
  ruleSummaries: RuleSummary[]
  lines: CalculationLine[]
}

export interface EmployeeCommissionSummary {
  employeeNo: string
  fullName: string
  department: string
  hotelCode: string
  grossSales: number
  refundTotal: number
  totalCommission: number
  calculatedAt: string | null
  isFinal: boolean
}

export interface PeriodSummary {
  year: number
  month: number
  status: 'Open' | 'Closed'
  closedAt: string | null
  closedBy: string | null
  employeeCount: number
  totalCommission: number
  employees: EmployeeCommissionSummary[]
}

export interface Period {
  year: number
  month: number
  status: 'Open' | 'Closed'
  closedAt: string | null
  closedBy: string | null
  saleCount: number
  calculatedEmployees: number
  totalCommission: number
}

export interface ImportBatch {
  id: number
  sourceSystem: SourceSystem
  fileName: string
  startedAt: string
  finishedAt: string | null
  totalRows: number
  importedRows: number
  duplicateRows: number
  errorRows: number
  importedBy: string
}

export interface ImportError {
  id: number
  lineNumber: number
  rawLine: string
  reason: string
  isDuplicate: boolean
}

export interface ImportResult {
  batch: ImportBatch
  errors: ImportError[]
}

export interface AuditLog {
  id: number
  entityName: string
  entityId: string
  action: 'Create' | 'Update' | 'Delete'
  oldValuesJson: string | null
  newValuesJson: string | null
  changedBy: string
  changedAt: string
}
