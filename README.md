### What has improved?

---

1. Chinese language files are added to make the whole process Chinese
2. The original author designed that all pictures of a notebook are stored in the same folder, and the folder path is: ".. /.. / \ _resource". But this is very inconvenient for me, so I implemented it. Each partition has an independent "_resource" folder to store image files.
3. Some Chinese comments have been added. Since I write in C language all the year round, the level of c# and code is limited. Some comments may be wrong. Please forgive me.

### Modify location:

---

1. Add "trad. Zh. JSON" files supporting Chinese language in the Resources folder

2. Getattachmentfilepathonpage (attachment attachment): defines a new function to migrate folders.

3. Void preparepageexport (page): this function is modified so that when creating a partition folder, a "page" is added at the same time\_ Resources folder“

4. Void exportpage (page page) in the exportservicebase.cs file comments out the original author's code so that it will not save the word document.

5. String getresourcefolderpath (node node): I changed the folder where I saved pictures to the folder where I stored files.