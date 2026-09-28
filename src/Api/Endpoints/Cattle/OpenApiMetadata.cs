// <copyright file="OpenApiMetadata.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Cattle;

/// <summary>
/// Contains OpenAPI metadata for cattle and holding endpoints.
/// </summary>
public static class OpenApiMetadata
{
    /// <summary>Gets the OpenAPI tag applied to cattle endpoints.</summary>
    public const string Tag = nameof(RouteNames.Cattle);

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
}
