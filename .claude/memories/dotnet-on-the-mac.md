---
name: dotnet-on-the-mac
description: .NET 10 lives in ~/.dotnet on Ernest's Mac, installed without sudo; put it on PATH per command
metadata:
  type: reference
---

`brew install --cask dotnet-sdk` needs a sudo password, so .NET 10 was installed with Microsoft's `dotnet-install.sh` into `~/.dotnet` (2026-10-23, #38). It isn't on the shell's PATH.

**Why:** the cask can't run from Claude's shell, and backend work needs `dotnet build`, `dotnet test` and `dotnet ef`.

**How to apply:** prefix backend commands with `export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`; run `dotnet tool restore` once for `dotnet ef`. There is no local PostgreSQL or `.env.local` on the Mac, so `[NeonFact]` tests skip here and CI runs them.
