
for file in *html; do
    tidy --vertical-space no -i -m $file
done
