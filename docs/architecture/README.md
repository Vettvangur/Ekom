# Architecture

[Ekom docs](../README.md) / Architecture

This section explains how Ekom is put together: which packages it has, what each one does, what happens when a site starts, and where Ekom keeps its data. Read it when you need to understand Ekom's behavior beyond one feature, for example to plan a deployment, diagnose a startup problem or decide which package a project needs.

To install Ekom, start with [Getting started](../getting-started/README.md) instead. You do not need this section to build a store.

## Where to start

Read [Overview](overview.md) first. It names the parts of Ekom and how a request and a content change move through them. The other two pages go into detail.

## Pages in this section

| Page | Read it when you want to |
| --- | --- |
| [Overview](overview.md) | See the layers of Ekom and how they work together at runtime. |
| [Packages](packages.md) | Find out which NuGet package contains what, and which ones a project needs. |
| [Startup and data](startup-and-data.md) | Know what Ekom does to the database and to memory when the site starts, and which data lives where. |

## Other sections

- [Getting started](../getting-started/README.md): install and configure Ekom.
- [Reference](../reference/README.md): the .NET API, HTTP API, events and settings.
- [Integrations](../integrations/README.md): the optional Algolia, Klaviyo and Mailchimp packages.
- [Contributing](../contributing/README.md): build and test Ekom from source.

[Back to the documentation home](../README.md)
