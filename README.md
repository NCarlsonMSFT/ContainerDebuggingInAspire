# An example of attaching to containers from Aspire

## Key Commits:
* [a77fb6b](https://github.com/NCarlsonMSFT/ContainerDebuggingInAspire/commit/a77fb6b6180da888856c4dfeefae0f9ed2368857) : This commit added a new VS configuration that builds OCI images for the projects and has Aspire runs them.
* [4bde686](https://github.com/NCarlsonMSFT/ContainerDebuggingInAspire/commit/4bde686b27b5a8294cdc62e88c959962227ad12d) : This commit adds auto-generating a certificate that supports localhost and host.docker.internal and using that to enable HTTPS communication between the containers and the Aspire host.
* [0a81faf](https://github.com/NCarlsonMSFT/ContainerDebuggingInAspire/commit/0a81fafed61e21626d03f8e1e7b00118f2d4330f) : This commit adds a helper to wait for the debugger / cert to be ready before starting service code
## Key Files:
* [ContainerAttacher.cs](ContainerDebugging.AppHost/ContainerAttacher.cs) : Helpers to configure the ContainerResource for Aspire and to orchestrate attaching to the running containers.
* [Certs](ContainerDebugging.AppHost/Certs) : Handles making the custom cert. Originally from [NCarlsonMSFT/CertExample](https://github.com/NCarlsonMSFT/CertExample).
