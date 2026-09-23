#!/usr/bin/env python3
"""Reference balances and fees from Trezor Suite CSV exports.

Deliberately independent of Capitrack's importer: plain Python, exact ``decimal.Decimal``
arithmetic, no shared code. It is the yardstick the importer is checked against.

Usage:
    python trezor_reference.py FILE.csv [FILE.csv ...] [--json]

Rules (Trezor Suite export semantics):
  * RECV  -> +Amount in "Amount unit".
  * SENT  -> -Amount in "Amount unit", plus the network fee.
  * SELF / FAILED / CONTRACT -> only the network fee leaves the wallet (the amount of a
    self-transfer comes straight back; a failed or pure contract call moves no value).
  * The fee is charged once per transaction, in "Fee unit" (which can differ from the amount's
    unit, e.g. a token transfer whose fee is paid in ETH). Suite prints it on the first row only.
  * One transaction can span several rows. On UTXO chains every row is a separate output and
    counts. On account-model chains the native coin leaves the wallet once per transaction (the
    transaction's value); further native-coin rows of the same SENT transaction are internal
    (contract) transfers and are excluded with a reason.
  * Non-numeric amounts (e.g. "ID 0" for an NFT) and rows that move nothing are excluded with
    a reason, never silently.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from decimal import Decimal, InvalidOperation

# Native coins whose ledger is account-based (one value transfer per transaction).
ACCOUNT_MODEL = {"ETH", "ETC", "BNB", "POL", "MATIC", "SOL", "XRP", "TRX", "AVAX", "ARB", "OP", "BASE"}
# Native coins whose ledger is UTXO-based (a transaction can pay several outputs).
UTXO_MODEL = {"BTC", "LTC", "DOGE", "BCH", "DASH", "ZEC", "DGB", "VTC", "NMC", "BTG", "ADA", "TEST", "REGTEST"}
FEE_ONLY_TYPES = {"SELF", "FAILED", "CONTRACT"}


def parse_number(text: str) -> Decimal | None:
    """Parses '1,284.75', '1.284,75', '0,5', '1e-8', '(3.2)'. Returns None when not a number."""
    s = (text or "").strip().replace(" ", "").replace(" ", "").replace("−", "-")
    if not s:
        return None
    negative = s.startswith("(") and s.endswith(")")
    s = s.strip("()")
    for sym in "$€£¥":
        s = s.replace(sym, "")
    if "," in s and "." in s:
        # the right-most separator is the decimal one
        s = s.replace(".", "").replace(",", ".") if s.rfind(",") > s.rfind(".") else s.replace(",", "")
    elif s.count(",") > 1:
        s = s.replace(",", "")
    elif "," in s:
        head, tail = s.split(",")
        # "1,305" (3 digits after, short head) is thousands; "0,5" is a decimal comma
        s = head + tail if len(tail) == 3 and head.lstrip("-").isdigit() and len(head.lstrip("-")) <= 3 and head.lstrip("-") != "0" else head + "." + tail
    try:
        value = Decimal(s)
    except InvalidOperation:
        return None
    return -value if negative else value


def main(argv: list[str]) -> int:
    paths = [a for a in argv if not a.startswith("--")]
    as_json = "--json" in argv
    if not paths:
        print(__doc__)
        return 2

    balance: dict[str, Decimal] = defaultdict(Decimal)
    fees: dict[str, Decimal] = defaultdict(Decimal)
    received: dict[str, Decimal] = defaultdict(Decimal)
    sent: dict[str, Decimal] = defaultdict(Decimal)
    movements: list[tuple[int, str, Decimal]] = []  # (timestamp, asset, signed delta) for the running check
    files: list[dict] = []

    for path in paths:
        with open(path, encoding="utf-8-sig", newline="") as fh:
            rows = list(csv.DictReader(fh))
        report = {"file": path.replace("\\", "/").split("/")[-1], "rows_read": len(rows), "counted": 0, "excluded": defaultdict(int)}

        by_tx: dict[str, list[dict]] = defaultdict(list)
        for r in rows:
            by_tx[r["Transaction ID"]].append(r)

        for txid, tx_rows in by_tx.items():
            fee_charged: set[str] = set()
            native_debited: set[str] = set()
            # the fee-bearing row is the transaction's primary target; process it first
            ordered = sorted(tx_rows, key=lambda r: 0 if (r.get("Fee") or "").strip() else 1)
            for r in ordered:
                kind = (r["Type"] or "").strip().upper()
                unit = (r["Amount unit"] or "").strip()
                ts = int(r["Timestamp"]) if (r.get("Timestamp") or "").strip().isdigit() else 0
                amount = parse_number(r["Amount"])
                fee = parse_number(r.get("Fee") or "")
                fee_unit = (r.get("Fee unit") or "").strip() or unit
                affects = False

                if kind not in {"RECV", "SENT"} | FEE_ONLY_TYPES:
                    report["excluded"][f"unsupported type {kind or '(blank)'}"] += 1
                    continue

                if kind in {"RECV", "SENT"}:
                    if amount is None:
                        report["excluded"]["non-numeric amount (NFT/token id)"] += 1
                        continue
                    amount = abs(amount)
                    if kind == "SENT" and unit in ACCOUNT_MODEL and unit in native_debited:
                        report["excluded"]["internal transfer (native coin already debited in this tx)"] += 1
                        continue
                    if amount != 0:
                        sign = Decimal(1) if kind == "RECV" else Decimal(-1)
                        balance[unit] += sign * amount
                        (received if kind == "RECV" else sent)[unit] += amount
                        movements.append((ts, unit, sign * amount))
                        affects = True
                    if kind == "SENT" and unit in ACCOUNT_MODEL:
                        native_debited.add(unit)

                # the network fee: paid by the wallet only for outgoing kinds, once per tx and unit
                if fee is not None and fee != 0 and kind != "RECV" and fee_unit not in fee_charged:
                    fee = abs(fee)
                    balance[fee_unit] -= fee
                    fees[fee_unit] += fee
                    movements.append((ts, fee_unit, -fee))
                    fee_charged.add(fee_unit)
                    affects = True

                if affects:
                    report["counted"] += 1
                else:
                    report["excluded"]["moves nothing (zero amount, no fee)"] += 1

        report["excluded"] = dict(report["excluded"])
        assert report["counted"] + sum(report["excluded"].values()) == report["rows_read"]
        files.append(report)

    # a wallet balance can never go negative: a negative running balance means a rule is wrong
    running: dict[str, Decimal] = defaultdict(Decimal)
    negative: dict[str, Decimal] = {}
    for ts, asset, delta in sorted(movements, key=lambda m: m[0]):
        running[asset] += delta
        if running[asset] < 0:
            negative[asset] = min(negative.get(asset, Decimal(0)), running[asset])

    assets = sorted(set(balance) | set(fees))
    result = {
        "assets": {a: {"balance": str(balance[a]), "fees": str(fees[a]), "received": str(received[a]), "sent": str(sent[a])} for a in assets},
        "files": files,
        "negative_running_balance": {a: str(v) for a, v in negative.items()},
    }
    if as_json:
        print(json.dumps(result, indent=2))
    else:
        print(f"{'asset':<10} {'balance':>28} {'total fees':>24}")
        for a in assets:
            print(f"{a:<10} {str(balance[a]):>28} {str(fees[a]):>24}")
        print()
        for f in files:
            print(f"{f['file']}: read {f['rows_read']} = counted {f['counted']} + excluded {sum(f['excluded'].values())} {f['excluded'] or ''}")
        print("negative running balance:", result["negative_running_balance"] or "none")
    return 1 if negative else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
