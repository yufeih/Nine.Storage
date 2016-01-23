#load "Common/scripts/build.csx"

BuildTestPublishPreRelease(
    projects: new []
    {
        "src/Nine.Storage.Abstractions",
        "src/Nine.Storage.Client",
        "src/Nine.Storage.Server",
        "test/Nine.Storage.Test",
    }, 
    testProjects: new []
    {
        "test/Nine.Storage.Client.Test",
        "test/Nine.Storage.Server.Test",
    });