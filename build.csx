#load "Common/scripts/build.csx"

BuildTestPublishPreRelease(
    additionalProjects: new [] { "test/Nine.Storage.Test" },
    suffix: "alpha1");
