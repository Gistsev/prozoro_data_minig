using Dapper;
using ProzorroAnalytics.Api.Domain;
using System.Data;
using System.Text.Json;

namespace ProzorroAnalytics.Api.Persistence;

public sealed class TenderRepository(IConfiguration configuration)
{
    public async Task UpsertAsync(TenderRecord tender, CancellationToken ct)
    {
        await using var connection = await Db.OpenConnectionAsync(configuration, ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        JsonDocument.Parse(tender.RawJson);

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO tenders(
                id,
                status,
                cpv_code,
                procuring_entity_name,
                expected_amount,
                created_at,
                date_modified,
                raw_json)
            VALUES (
                @Id,
                @Status,
                @CpvCode,
                @ProcuringEntityName,
                @ExpectedAmount,
                @CreatedAt,
                @DateModified,
                @RawJson::jsonb)
            ON CONFLICT (id) DO UPDATE SET
                status = EXCLUDED.status,
                cpv_code = EXCLUDED.cpv_code,
                procuring_entity_name = EXCLUDED.procuring_entity_name,
                expected_amount = EXCLUDED.expected_amount,
                created_at = EXCLUDED.created_at,
                date_modified = EXCLUDED.date_modified,
                raw_json = EXCLUDED.raw_json,
                imported_at = NOW();
            """,
            new
            {
                tender.Id,
                tender.Status,
                tender.CpvCode,
                tender.ProcuringEntityName,
                tender.ExpectedAmount,
                CreatedAt = tender.CreatedAt?.UtcDateTime,
                DateModified = tender.DateModified?.UtcDateTime,
                tender.RawJson
            },
            tx,
            commandTimeout: 30,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM tender_contracts WHERE tender_id = @Id;
            """, tender, tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM tender_suppliers WHERE tender_id = @Id;
            """, tender, tx, cancellationToken: ct));

        if (tender.Contracts.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO tender_contracts(tender_id, contract_external_id, amount)
                VALUES (@TenderId, @ExternalId, @Amount);
                """, tender.Contracts.Select(c => new { TenderId = tender.Id, c.ExternalId, c.Amount }), tx, cancellationToken: ct));
        }

        if (tender.Suppliers.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO tender_suppliers(tender_id, supplier_name, award_external_id, amount)
                VALUES (@TenderId, @SupplierName, @AwardExternalId, @Amount);
                """, tender.Suppliers.Select(s => new { TenderId = tender.Id, s.SupplierName, s.AwardExternalId, s.Amount }), tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }

    public async Task<AnalyticsSummary> GetSummaryAsync(CancellationToken ct)
    {
        await using var connection = await Db.OpenConnectionAsync(configuration, ct);

        var totalSaving = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT COALESCE(SUM(saving), 0) FROM tender_contract_totals;
            """, cancellationToken: ct));

        var buyers = (await connection.QueryAsync<TopAnalyticsRow>(new CommandDefinition("""
            SELECT procuring_entity_name AS Name, COALESCE(SUM(contracts_amount), 0) AS Amount
            FROM tender_contract_totals
            GROUP BY procuring_entity_name
            ORDER BY Amount DESC
            LIMIT 5;
            """, cancellationToken: ct))).AsList();

        var suppliers = (await connection.QueryAsync<TopAnalyticsRow>(new CommandDefinition("""
            SELECT supplier_name AS Name, COALESCE(SUM(amount), 0) AS Amount
            FROM tender_suppliers
            GROUP BY supplier_name
            ORDER BY Amount DESC
            LIMIT 5;
            """, cancellationToken: ct))).AsList();

        return new AnalyticsSummary(totalSaving, buyers, suppliers);
    }
}
