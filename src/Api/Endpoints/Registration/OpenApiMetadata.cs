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
        public const string Summary = "Submit a cattle registration bundle.";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Checks the shape of a bundle of cattle registrations and queues it, returning the bundle ID at once. " +
            "The bundle is then stored and sent to CTS as one Birth request in the background.";
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
