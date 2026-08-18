client.test("No Content result with 204", () => {
    client.assert(response.status === 204, "HTTP Response status code is not 204");
});