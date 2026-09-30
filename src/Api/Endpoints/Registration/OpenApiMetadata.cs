// <copyright file="OpenApiMetadata.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Registration;

/// <summary>
/// Contains OpenAPI metadata for cattle registration endpoints.
/// </summary>
public static class OpenApiMetadata
{
    /// <summary>Gets the OpenAPI tag applied to registration endpoints.</summary>
    public const string Tag = nameof(RouteNames.Registrations);

    /// <summary>Contains metadata for the create-registration-bundle endpoint.</summary>
    public static class CreateRegistrationBundleRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "CreateRegistrationBundle";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Create a cattle registration bundle.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Creates a bundle of cattle registrations for asynchronous validation and processing.";
    }

    /// <summary>Contains metadata for the validate-registration-bundle endpoint.</summary>
    public static class ValidateRegistrationBundleRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "ValidateRegistrationBundle";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Validate a cattle registration bundle.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Validates the cattle registration bundle identified by its unique ID.";
    }
}
