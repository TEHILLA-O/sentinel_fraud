using FluentAssertions;
using Sentinel.Contracts;
using Sentinel.Domain.Tests;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.SharedKernel;

namespace Sentinel.RuleTests;

public class HighValueRuleTests
{
    private readonly HighValueRule _rule = new();

    [Fact]
    public async Task Positive_when_amount_exceeds_threshold()
    {
        var result = await _rule.EvaluateAsync(RiskContextFactory.Example(4850m), CancellationToken.None);
        result.Triggered.Should().BeTrue();
        result.Score.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public async Task Negative_when_amount_is_normal()
    {
        var result = await _rule.EvaluateAsync(RiskContextFactory.Example(40m), CancellationToken.None);
        result.Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Boundary_at_exact_threshold()
    {
        var result = await _rule.EvaluateAsync(RiskContextFactory.Example(3000m), CancellationToken.None);
        result.Triggered.Should().BeTrue();
        result.Score.Should().Be(20);
    }

    [Fact]
    public async Task Edge_just_below_threshold()
    {
        var result = await _rule.EvaluateAsync(RiskContextFactory.Example(2999.99m), CancellationToken.None);
        result.Triggered.Should().BeFalse();
    }
}

public class HighVelocityRuleTests
{
    private readonly HighVelocityRule _rule = new();

    [Fact]
    public async Task Positive_when_five_minute_count_is_high()
    {
        var context = RiskContextFactory.Example(velocity: v => v with { TransactionsLast5Minutes = 7, TransactionsLast1Minute = 2 });
        var result = await _rule.EvaluateAsync(context, CancellationToken.None);
        result.Triggered.Should().BeTrue();
        result.Score.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public async Task Negative_when_velocity_is_quiet()
    {
        var result = await _rule.EvaluateAsync(RiskContextFactory.Example(), CancellationToken.None);
        result.Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Boundary_at_configured_count()
    {
        var context = RiskContextFactory.Example(velocity: v => v with { TransactionsLast5Minutes = 5 });
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeTrue();
    }

    [Fact]
    public async Task Edge_one_below_limit()
    {
        var context = RiskContextFactory.Example(velocity: v => v with { TransactionsLast5Minutes = 4, TransactionsLast1Minute = 2 });
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeFalse();
    }
}

public class ForeignCountryRuleTests
{
    private readonly ForeignCountryRule _rule = new();

    [Fact]
    public async Task Positive_for_unusual_country() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(country: "US"), CancellationToken.None)).Triggered.Should().BeTrue();

    [Fact]
    public async Task Negative_for_home_country() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(country: "GB"), CancellationToken.None)).Triggered.Should().BeFalse();

    [Fact]
    public async Task Boundary_common_country_is_accepted()
    {
        var context = RiskContextFactory.Example(country: "IE", profile: p => p with { CommonCountries = ["GB", "IE"] });
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Edge_case_insensitive_home_match() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(country: "gb"), CancellationToken.None)).Triggered.Should().BeFalse();
}

public class UnknownDeviceRuleTests
{
    private readonly UnknownDeviceRule _rule = new();

    [Fact]
    public async Task Positive_unknown_device() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(deviceId: "D-NEW"), CancellationToken.None)).Triggered.Should().BeTrue();

    [Fact]
    public async Task Negative_known_device() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(deviceId: "D-UK-4411"), CancellationToken.None)).Triggered.Should().BeFalse();

    [Fact]
    public async Task Boundary_missing_device_is_unknown() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(deviceId: null), CancellationToken.None)).Triggered.Should().BeTrue();

    [Fact]
    public async Task Edge_device_in_profile_common_list()
    {
        var context = RiskContextFactory.Example(deviceId: "D-SECOND", profile: p => p with { CommonDevices = ["D-SECOND"] });
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeFalse();
    }
}

public class CardNotPresentRuleTests
{
    private readonly CardNotPresentRule _rule = new();

    [Fact]
    public async Task Positive_web_cnp() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(channel: Channel.WEB, cardPresent: false), CancellationToken.None)).Triggered.Should().BeTrue();

    [Fact]
    public async Task Negative_pos_present() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(), CancellationToken.None)).Triggered.Should().BeFalse();

    [Fact]
    public async Task Boundary_transfer_is_ignored() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(channel: Channel.TRANSFER, cardPresent: false), CancellationToken.None)).Triggered.Should().BeFalse();

    [Fact]
    public async Task Edge_atm_without_card_still_flags() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(channel: Channel.ATM, cardPresent: false), CancellationToken.None)).Triggered.Should().BeTrue();
}

public class ImpossibleTravelRuleTests
{
    private readonly ImpossibleTravelRule _rule = new();

    [Fact]
    public async Task Positive_london_to_new_york_in_45_minutes()
    {
        var now = new DateTimeOffset(2026, 9, 12, 14, 45, 0, TimeSpan.Zero);
        var recent = new[]
        {
            new HistoricalTransaction
            {
                TransactionId = "TX-A",
                Country = "GB",
                City = "London",
                Timestamp = now.AddMinutes(-45),
                Channel = Channel.POS,
                Amount = Money.Gbp(20),
                Approved = true
            }
        };
        var context = RiskContextFactory.Example(country: "US", channel: Channel.ATM, timestamp: now, recent: recent);
        var result = await _rule.EvaluateAsync(context, CancellationToken.None);
        result.Triggered.Should().BeTrue();
        result.Score.Should().Be(40);
    }

    [Fact]
    public async Task Negative_for_web_channel()
    {
        var now = new DateTimeOffset(2026, 9, 12, 14, 45, 0, TimeSpan.Zero);
        var recent = new[]
        {
            new HistoricalTransaction
            {
                TransactionId = "TX-A",
                Country = "GB",
                City = "London",
                Timestamp = now.AddMinutes(-45),
                Channel = Channel.POS,
                Amount = Money.Gbp(20),
                Approved = true
            }
        };
        var context = RiskContextFactory.Example(country: "US", channel: Channel.WEB, cardPresent: false, timestamp: now, recent: recent);
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Boundary_no_previous_physical_transaction() =>
        (await _rule.EvaluateAsync(RiskContextFactory.Example(country: "US", channel: Channel.POS), CancellationToken.None)).Triggered.Should().BeFalse();

    [Fact]
    public async Task Edge_slow_travel_is_possible()
    {
        var now = new DateTimeOffset(2026, 9, 12, 14, 45, 0, TimeSpan.Zero);
        var recent = new[]
        {
            new HistoricalTransaction
            {
                TransactionId = "TX-A",
                Country = "GB",
                City = "London",
                Timestamp = now.AddHours(-10),
                Channel = Channel.POS,
                Amount = Money.Gbp(20),
                Approved = true
            }
        };
        var context = RiskContextFactory.Example(country: "US", channel: Channel.POS, timestamp: now, recent: recent);
        (await _rule.EvaluateAsync(context, CancellationToken.None)).Triggered.Should().BeFalse();
    }
}

public class RemainingRuleTests
{
    [Fact]
    public async Task Merchant_risk_positive_and_negative()
    {
        var rule = new MerchantRiskRule();
        var risky = RiskContextFactory.Example();
        risky = risky with
        {
            Transaction = risky.Transaction with { MerchantCategory = MerchantCategory.Crypto }
        };
        (await rule.EvaluateAsync(risky, CancellationToken.None)).Triggered.Should().BeTrue();
        (await rule.EvaluateAsync(RiskContextFactory.Example(), CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Unusual_time_uses_profile_hours()
    {
        var rule = new UnusualTimeRule();
        var odd = RiskContextFactory.Example(timestamp: new DateTimeOffset(2026, 9, 12, 3, 0, 0, TimeSpan.Zero));
        (await rule.EvaluateAsync(odd, CancellationToken.None)).Triggered.Should().BeTrue();
        var normal = RiskContextFactory.Example(timestamp: new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));
        (await rule.EvaluateAsync(normal, CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task New_account_boundary()
    {
        var rule = new NewAccountRule();
        // Anchor to the factory's fixed EvaluatedAt so the test does not drift with the wall clock.
        var evaluatedAt = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var young = RiskContextFactory.Example(profile: p => p with { AccountOpenedAt = evaluatedAt.AddDays(-3) });
        (await rule.EvaluateAsync(young, CancellationToken.None)).Triggered.Should().BeTrue();
        var mature = RiskContextFactory.Example(profile: p => p with { AccountOpenedAt = evaluatedAt.AddDays(-30) });
        (await rule.EvaluateAsync(mature, CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Behaviour_deviation_ratio()
    {
        var rule = new BehaviourDeviationRule();
        (await rule.EvaluateAsync(RiskContextFactory.Example(4850m), CancellationToken.None)).Triggered.Should().BeTrue();
        (await rule.EvaluateAsync(RiskContextFactory.Example(80m), CancellationToken.None)).Triggered.Should().BeFalse();
        var thin = RiskContextFactory.Example(4850m, profile: p => p with { LifetimeTransactionCount = 2 });
        (await rule.EvaluateAsync(thin, CancellationToken.None)).Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Rapid_country_and_declines_and_round_amount()
    {
        var countries = new RapidCountryChangeRule();
        var declines = new RepeatedDeclineRule();
        var round = new RoundAmountRule();

        var hot = RiskContextFactory.Example(velocity: v => v with { DistinctCountriesLast30Minutes = 3, FailedTransactionsLast15Minutes = 4 });
        (await countries.EvaluateAsync(hot, CancellationToken.None)).Triggered.Should().BeTrue();
        (await declines.EvaluateAsync(hot, CancellationToken.None)).Triggered.Should().BeTrue();
        (await round.EvaluateAsync(RiskContextFactory.Example(1000m), CancellationToken.None)).Triggered.Should().BeTrue();
        (await round.EvaluateAsync(RiskContextFactory.Example(1000.50m), CancellationToken.None)).Triggered.Should().BeFalse();
        (await countries.EvaluateAsync(RiskContextFactory.Example(), CancellationToken.None)).Triggered.Should().BeFalse();
    }
}
