client.test("Conflict result with 409", () => {
    client.assert(response.status === 409, "HTTP Response status code is not 409");
});