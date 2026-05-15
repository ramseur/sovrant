---
name: security-review
description: OWASP-based security audit
trigger: /security
agents: [security-reviewer]
tools: [Read, Grep, Glob, Bash, WebSearch]
---

# Security Review

Systematic security audit based on OWASP Top 10 and common vulnerability patterns.

## Steps
1. **Scope** — identify the attack surface (APIs, auth, data flows, dependencies)
2. **OWASP Top 10 scan** — check each category:
   - A01: Broken Access Control
   - A02: Cryptographic Failures
   - A03: Injection (SQL, XSS, command)
   - A04: Insecure Design
   - A05: Security Misconfiguration
   - A06: Vulnerable Components
   - A07: Authentication Failures
   - A08: Data Integrity Failures
   - A09: Logging/Monitoring Failures
   - A10: SSRF
3. **Dependency audit** — check for known CVEs in dependencies
4. **Secret scan** — search for hardcoded credentials, keys, tokens
5. **Report** — structured findings with severity and remediation

## Output Format
| Finding | OWASP | Severity | Location | Remediation |
|---------|-------|----------|----------|-------------|
| ...     | A0X   | Critical | file:line | ... |

## Rules
- Never dismiss a finding without explanation
- Provide specific remediation steps, not just "fix this"
- Check both code and configuration
- Include false-positive assessment for uncertain findings
