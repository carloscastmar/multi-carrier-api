# 1. Use an idempotency key for retrying requests

## Status
Proposed

## Context
Discussion about whether we should use idempotency keys for retrying shipment requests or not

## Decision
Potentially, we should only accept idempotent requests if the body is the same. Further discussion needed.

## Consequences
If idempotency keys are used on different bodies, we take the risk of providing a wrong label.
