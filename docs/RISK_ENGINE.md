# Risk engine

The production path is deterministic.

```csharp
public interface IRiskRule
{
    string Code { get; }
    ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken);
}
```

`DeterministicRiskEngine` evaluates every enabled rule, sums scores, caps at 100, and asks `ThresholdDecisionPolicy` for APPROVE / REVIEW / BLOCK.

## Score bands

| Score | Level | Decision |
| --- | --- | --- |
| 0-29 | LOW | APPROVE |
| 30-59 | MEDIUM | APPROVE, or REVIEW when velocity / impossible travel / repeated decline fired |
| 60-79 | HIGH | REVIEW |
| 80-100 | CRITICAL | BLOCK |

## Model combination

`IRiskModel` is optional. `NoOpRiskModel` is the default. `MlNetAnomalyRiskModel` can be registered with `Risk:UseMlNet=true`. Model points are added only when confidence ≥ 0.6 and are capped at 15.

Every evaluation records `RulesetVersion` and `ModelVersion`.
