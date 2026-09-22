namespace Vantage.Approvals.Core.Storage;

/// <summary>
/// Physical table names. Six tables: four aggregates plus two denormalized indexes.
/// </summary>
internal static class TableNames
{
    internal const string Reviews = "reviews";
    internal const string Decisions = "reviewdecisions";
    internal const string Notes = "reviewnotes";
    internal const string Assignments = "reviewassignments";

    /// <summary>Index: latest round per campaign.</summary>
    internal const string AssignmentsByCampaign = "reviewassignmentsbycampaign";

    /// <summary>Index: outstanding rounds per client.</summary>
    internal const string AssignmentsByClient = "reviewassignmentsbyclient";
}
