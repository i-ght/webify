
for file in *html; do
    tidy -i -m $file
done
