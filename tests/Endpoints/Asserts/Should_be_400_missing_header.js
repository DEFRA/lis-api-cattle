client.test("Should be 400 Bad Request", () => {
    client.assert(response.status === 400, "HTTP Response status code is not 400");
});

client.test("Problem details code is missing_header", () => {
    client.assert(response.body.code === "missing_header", "Expected code 'missing_header' but received '" + response.body.code + "'");
});
