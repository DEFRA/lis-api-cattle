
// Should be JSON Response
client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

// Should_be_200_OK.js (or your response handler file)
client.test("Extract the pages item ids", function () {
    // If your response is JSON array like: [{ id: 123, ... }, ...]
    const items = response.body;

    client.assert(Array.isArray(items), "Expected response to be an array of items");
    client.assert(items.length > 0, "Expected at least one item");

    // Store for the next request:
    const ids = items.map(item => item.id);
    client.global.set("ids", ids);
    client.assert(ids.length > 0, "Expected item to have an id");
});



