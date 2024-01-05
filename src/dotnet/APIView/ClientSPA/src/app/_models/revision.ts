import { ChangeHistory } from "./review"

export interface Revision {
  id: string
  reviewId: string
  packageName: string
  language: string
  apiRevisionType: string
  label: string
  resolvedLabel: string
  packageVersion: string
  isApproved: boolean
  createdBy: string
  createdOn: string
  lastUpdatedOn: string
  isDeleted: boolean
}
  
  