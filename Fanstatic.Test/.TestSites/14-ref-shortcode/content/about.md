---
Title: About
---

Absolute: [hello]({{< ref "posts/hello.md" >}})

Relative: [hello]({{< relref "posts/hello.md" >}})

Anchor: {{< ref "posts/hello.md#section" >}}

Portuguese: {{< ref "posts/hello.md" lang="pt-br" >}}

RSS: {{< ref "posts/hello.md" outputFormat="rss" >}}

Missing: {{< ref "posts/does-not-exist.md" >}}
