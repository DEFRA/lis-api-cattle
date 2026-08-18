
// Should be JSON Response
client.test("Response content-type is json", () => {
    var contentType = response.contentType.mimeType;
    client.assert(contentType === "application/json", "Expected content-type 'application/json' but received '" + contentType + "'");
});

// Should_be_200_OK.js (or your response handler file)
client.test("Has 5 paged results", function () {
    
    const pagedResult = response.body;
    
    client.assert(!Array.isArray(pagedResult), "Expected item to be a paged response object");
    
    const items = pagedResult.items;

    client.assert(Array.isArray(items), "Expected paged response items to be an array of items");
    client.assert(items.length === 5, "Expected 5 items in the paged response");

    client.assert(pagedResult && pagedResult.total_count != null, "Expected paged result to have total count");
    client.assert(pagedResult && pagedResult.total_pages != null, "Expected paged result to have total pages");
    client.assert(pagedResult && pagedResult.page_number != null, "Expected paged result to have page number");
    client.assert(pagedResult && pagedResult.page_number != null, "Expected paged result to have page size");
});



