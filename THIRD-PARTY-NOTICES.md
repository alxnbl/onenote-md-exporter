# Third-party notices

This project is licensed under the GNU General Public License v3.0 (see `LICENSE`). NuGet dependencies
carry their own licences, declared in their packages.

## segunak/one-note-to-markdown (MIT)

`src/OneNoteMdExporter/Services/XmlMarkdownRenderer.cs` — the XML-direct Markdown renderer — was written
for this project, but its design was studied from
[segunak/one-note-to-markdown](https://github.com/segunak/one-note-to-markdown), file
`OneNoteMarkdownExporter/Services/OneNoteXmlToMarkdownConverter.cs`.

Ideas taken from it: the element handling order (`one:Outline` > `one:OEChildren` > `one:OE`, detecting
runs of list items, joining the paragraphs of a table cell with `<br>`) and the principle of protecting
literal text so no original character can be read as Markdown syntax.

No code was copied. That project builds an intermediate HTML document with HtmlAgilityPack and converts
it with ReverseMarkdown; this renderer has no dependencies at all and writes Markdown directly. Its
licence is reproduced here because the MIT licence requires the notice to travel with derived work, and
because the inspiration is worth crediting either way. MIT is compatible with GPL-3.0.

```
MIT License

Copyright (c) 2025 Segun Akinyemi

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## MiniSearch (MIT)

`tools/vendor/minisearch.umd.js` is [MiniSearch](https://github.com/lucaong/minisearch) by Luca Ongaro,
bundled unmodified so that `tools/build-search.ps1` can build an offline full-text search page for an
exported archive that works in any browser with no server and no internet. Its licence is in
`tools/vendor/minisearch.LICENSE.txt` and reproduced here.

```
Copyright 2022 Luca Ongaro

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the
following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO
EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR
THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
