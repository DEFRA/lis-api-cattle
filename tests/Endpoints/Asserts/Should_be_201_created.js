client.test("Should be 201 Created", () => {
    client.assert(response.status === 201, "HTTP Response status code is not 201");
});

client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

client.test("Response body has id", () => {
    client.assert(response.body.hasOwnProperty("id"), "Response body missing 'id'");
    client.global.set("id", response.body.id);
});

