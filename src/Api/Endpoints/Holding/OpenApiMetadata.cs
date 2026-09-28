// <copyright file="OpenApiMetadata.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Holding;

/// <summary>
/// Contains OpenAPI metadata for cattle and holding endpoints.
/// </summary>
public static class OpenApiMetadata
{
    /// <summary>Gets the OpenAPI tag applied to cattle endpoints.</summary>
    public const string Tag = nameof(RouteNames.Holdings);

    /// <summary>Contains metadata for the get-holding endpoint.</summary>
    public static class GetHoldingRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetHolding";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the holding details";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the details of the holding for the given county, parish and holding number";
    }

    /// <summary>Contains metadata for the multi-segment get-cattle endpoint.</summary>
    public static class GetCattleForHoldingMultiSegmentRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetCattleForHoldingMultiSegment";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the cattle on the holding.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the cattle for the given county, parish and holding number.";
    }

    /// <summary>Contains metadata for the CPH-based get-cattle endpoint.</summary>
    public static class GetCattleForHoldingRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetCattleForHolding";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the cattle on the holding.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the cattle for the given CPH number.";
    }

    /// <summary>Contains metadata for the multi-segment get-bundles endpoint.</summary>
    public static class GetBundlesForHoldingMultiSegmentRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetBundlesForHoldingMultiSegment";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the bundles for the holding.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the list of bundles for the given county, parish and holding number in the route.";
    }

    /// <summary>Contains metadata for the CPH-based get-bundles endpoint.</summary>
    public static class GetBundlesForHoldingRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetBundlesForHolding";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the bundles for the holding.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the registration bundles for the given CPH number.";
    }
}
