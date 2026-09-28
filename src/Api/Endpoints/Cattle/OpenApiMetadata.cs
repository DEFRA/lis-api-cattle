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
    public static class GetCattleDetailsRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetCattleDetails";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the cattle details";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves the details of the cattle for the given county, parish and holding number";
    }
}
