// <copyright file="OpenApiMetadata.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Users;

/// <summary>
/// Contains OpenAPI metadata for user endpoints.
/// </summary>
public static class OpenApiMetadata
{
    /// <summary>Gets the OpenAPI tag applied to user endpoints.</summary>
    public const string Tag = nameof(RouteNames.Users);

    /// <summary>Contains metadata for the get-user-details endpoint.</summary>
    public static class GetUserDetailsRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetUserDetails";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get a user's details and associated CPHs";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Retrieves a user's details and the CPHs they are associated with from the keeper data service, " +
            "by identity provider subject. The associations are those captured when the user last signed in.";
    }

    /// <summary>Contains metadata for the create-user endpoint.</summary>
    public static class CreateUserRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "CreateUser";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Create or refresh a user account";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description =
            "Ensures a user account exists in the keeper data service for the supplied identity provider claims " +
            "(sub, email, given_name, family_name) and returns the user's details and associated CPHs. An existing " +
            "account (matched by subject, or by an email with no subject yet) is refreshed; otherwise one is created. " +
            "Returns 409 when the email belongs to a different account and 503 when the keeper data service is " +
            "temporarily unavailable.";
    }
}
