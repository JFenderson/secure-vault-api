# ADR 001: Use RBAC authorization for Key Vault over access policies

## Status
Accepted

## Context
Azure Key Vault supports two permission models: vault access policies 
and Azure RBAC. A decision was needed before any secrets were stored.

## Decision
Use Azure RBAC authorization (--enable-rbac-authorization true).

## Rationale
- RBAC integrates with Azure AD audit logs; access policy grants do not 
  appear in the same unified activity log
- Role assignments can be reviewed with standard IAM tooling across all 
  Azure resources, not vault-by-vault
- RBAC supports Conditional Access policies; legacy access policies do not
- Aligns with Microsoft's current recommended practice for new vaults

## Consequences
- Role assignments require Azure AD permissions to manage
- Slightly more setup than access policies initially
- All team members (or automated agents) need explicit role assignments
