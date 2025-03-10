# An example of attaching to containers from Aspire

## Key Commits:
* [a77fb6b](https://github.com/NCarlsonMSFT/ContainerDebuggingInAspire/commit/a77fb6b6180da888856c4dfeefae0f9ed2368857) : This commit added a new VS configuration that builds OCI images for the projects and has Aspire runs them.
* [4bde686](https://github.com/NCarlsonMSFT/ContainerDebuggingInAspire/commit/4bde686b27b5a8294cdc62e88c959962227ad12d) : This commit adds auto-generating a certificate that supports localhost and host.docker.internal and using that to enable HTTPS communication between the contaienrs and the Aspire host.

## Key Files:
* [ContainerAttacher.cs](ContainerDebugging.AppHost/ContainerAttacher.cs) : Helpers to configure the ContainerResource for Aspire and to orchestarte attaching to the running containers.
* [Certs](ContainerDebugging.AppHost/Certs) : Handles making the custom cert. Originally from [NCarlsonMSFT/CertExample](https://github.com/NCarlsonMSFT/CertExample).
