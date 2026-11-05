using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>Seeds default severity override rules without overwriting user changes.</summary>
    public sealed class SeverityRuleSeeder : ISeverityRuleSeeder
    {
        private readonly ISeverityRuleRepository _severityRuleRepository;

        /// <summary>Initialises a new seeder.</summary>
        public SeverityRuleSeeder(ISeverityRuleRepository severityRuleRepository)
        {
            _severityRuleRepository = severityRuleRepository;
        }

        /// <summary>Inserts default rules when none are configured. Returns the number inserted.</summary>
        public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SeverityRule> existing = await _severityRuleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            if (existing.Count > 0)
            {
                return 0;
            }

            DateTime now = DateTime.UtcNow;

            SeverityRule typoRule = new()
            {
                Order = Consts.DefaultSeverityRuleOrder,
                Name = Consts.DefaultSeverityRuleTypoName,
                MatchText = Consts.DefaultSeverityRuleTypoMatchText,
                MatchField = SeverityRuleMatchField.Both,
                Action = SeverityRuleAction.Cap,
                TargetSeverity = FindingSeverity.Medium,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };

            SeverityRule namingRule = new()
            {
                Order = Consts.DefaultSeverityRuleOrder + Consts.SeverityRuleOrderStep,
                Name = Consts.DefaultSeverityRuleNamingInconsistencyName,
                MatchText = Consts.DefaultSeverityRuleNamingInconsistencyMatchText,
                MatchField = SeverityRuleMatchField.Both,
                Action = SeverityRuleAction.Cap,
                TargetSeverity = FindingSeverity.Medium,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };

            await _severityRuleRepository.AddAsync(typoRule, cancellationToken).ConfigureAwait(false);
            await _severityRuleRepository.AddAsync(namingRule, cancellationToken).ConfigureAwait(false);

            return Consts.DefaultSeverityRuleSeedCount;
        }
    }
}
