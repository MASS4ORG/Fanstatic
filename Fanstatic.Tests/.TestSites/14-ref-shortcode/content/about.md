---
Title: About
---

Absolute: [hello]({{< ref "posts/hello.md" >}})

Relative: [hello]({{< relref "posts/hello.md" >}})

Anchor: {{< ref "posts/hello.md#section" >}}

Portuguese: {{< ref "posts/hello.md" lang="pt-br" >}}

RSS: {{< ref "posts/hello.md" outputFormat="rss" >}}

Untranslated: {{< ref "posts/greeting.md" lang="pt-br" >}}

Missing: {{< ref "posts/does-not-exist.md" >}}

RootPath: {{< ref "/posts/hello.md" >}}

ContentPath: {{< ref "/content/posts/hello.md" >}}

ResourcePath: {{< ref "posts/gallery/cover.jpg" >}}

ResourceRelPath: {{< relref "posts/gallery/cover.jpg" >}}

ResourceContentPath: {{< ref "/content/posts/gallery/cover.jpg" >}}

BundleIndex: {{< ref "posts/gallery/index.md" >}}

BundleBranch: {{< ref "posts/_index.md" >}}

Home: {{< ref "index.md" >}}

HomeRoot: {{< ref "/" >}}

SelfAnchor: {{< ref "#section" >}}

ResourceAnchor: {{< ref "posts/gallery/cover.jpg#figure" >}}

RelRootAnchor: {{< relref "/content/posts/hello.md#root" >}}

ResourceLangFallback: {{< ref "posts/gallery/cover.jpg" lang="de" >}}

ResourceFormatFallback: {{< ref "posts/gallery/cover.jpg" outputFormat="rss" >}}
