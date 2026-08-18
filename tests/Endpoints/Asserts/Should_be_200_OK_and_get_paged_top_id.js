
// Should be JSON Response
client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

// Should_be_200_OK.js (or your response handler file)
client.test("Extract top paged item id", function () {

    const pagedResult = response.body;
    
    client.assert(!Array.isArray(pagedResult), "Expected item to be a paged response object");
    
    const items = pagedResult.items;

    client.assert(Array.isArray(items), "Expected paged response items to be an array of items");
    client.assert(items.length > 0, "Expected at least one paged response item");

    const top = items[0];
    
    client.assert(top && top.id != null, "Expected top item to have an id");

    // Store for the next request:
    client.global.set("id", String(top.id));
});



