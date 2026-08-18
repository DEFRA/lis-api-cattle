
// Should be JSON Response
client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

// Should_be_200_OK.js (or your response handler file)
client.test("Extract top item id", function () {
    // If your response is JSON array like: [{ id: 123, ... }, ...]
    const items = response.body;

    client.assert(Array.isArray(items), "Expected response to be an array of items");
    client.assert(items.length > 0, "Expected at least one item");

    // Pick "top" however you define it. Common options:
    // 1) first item:
    const top = items[0];

    // 2) OR max id:
    // const top = users.reduce((max, u) => (u.id > max.id ? u : max), items[0]);

    client.assert(top && top.id != null, "Expected item to have an id");

    // Store for the next request:
    client.global.set("id", String(top.id));
    client.global.set("operator-id", top.id);
});



