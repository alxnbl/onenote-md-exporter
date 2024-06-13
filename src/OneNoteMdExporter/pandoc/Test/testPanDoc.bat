pandoc.exe test.docx -o "exp/test_markdown.md"         --extract-media="exp/markdown"         --wrap=none --to markdown
pandoc.exe test.docx -o "exp/test_commonmark.md"       --extract-media="exp/commonmark"       --wrap=none --to commonmark
pandoc.exe test.docx -o "exp/test_commonmark_x.md"     --extract-media="exp/commonmark_x"     --wrap=none --to commonmark_x
pandoc.exe test.docx -o "exp/test_gfm.md"              --extract-media="exp/gfm"              --wrap=none --to gfm
pandoc.exe test.docx -o "exp/test_gfm_styles.md"       --extract-media="exp/gfm_styles"       --wrap=none --to gfm                                                  --from docx+styles
pandoc.exe test.docx -o "exp/test_gfm2.md"             --extract-media="exp/gfm2"             --wrap=none --to gfm+bracketed_spans+raw_attribute+subscript-raw_html
pandoc.exe test.docx -o "exp/test_gfm2_styles.md"      --extract-media="exp/gfm2_styles"      --wrap=none --to gfm+bracketed_spans+raw_attribute+subscript-raw_html --from docx+styles
pandoc.exe test.docx -o "exp/test_markdown2.md"        --extract-media="exp/markdown2"        --wrap=none --to markdown+mark+all_symbols_escapable
pandoc.exe test.docx -o "exp/test_markdown_strict.md"  --extract-media="exp/markdown_strict"  --wrap=none --to markdown_strict
pandoc.exe test.docx -o "exp/test_markdown_strict2.md" --extract-media="exp/markdown_strict2" --wrap=none --to markdown_strict+mark+emoji+all_symbols_escapable+grid_tables+markdown_attribute+pipe_tables+strikeout
pandoc.exe test.docx -o "exp/test_markdown_strict3.md" --extract-media="exp/markdown_strict3" --wrap=none --to markdown_strict+emoji+gfm_auto_identifiers+grid_tables+markdown_attribute+multiline_tables+pipe_tables+simple_tables+strikeout
