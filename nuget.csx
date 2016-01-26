#load "Common/scripts/build.csx"

BuildTestPublishPreRelease(
    projects: new []
    {
        "src/*/",
        "test/Nine.Storage.Test",
    },
    testProjects: new []
    {
        "test/Nine.Storage.Client.Test",
        "test/Nine.Storage.Server.Test",
    });