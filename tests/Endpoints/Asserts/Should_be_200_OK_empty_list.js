client.test("Successful request with OK 200", () => {
    client.assert(response.status === 200, "HTTP Response status code is not 200");
});

client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

client.test("Response body is empty array", () => {
    var body = response.body;
    client.assert(Array.isArray(body), "Response body is not an array");
    client.assert(body.length === 0, "Response body array is not empty, length: " + body.length);
});


