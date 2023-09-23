Long text

mardi 10 novembre 2020

08:54

 

> **Étape 1** – Établir des bases communes entre vous, votre auditoire et le projet.
>
>  

# Evolution d'internet

>  

## Genèse

>  
>
> <img src="exp/commonmark/media/image1.jpg" style="width:6.24167in;height:3.33333in" alt="Pin on GeoWeb" />
>
>  
>
> Début d'Internet : scientifiques puis geek
>
> Décentralisé, technologie pas matures qui permettaient de faire des choses très basiques
>
> HTTP et MAIL
>
>  

## Evolution

>  
>
> Technologies ont gagnées en maturité
>
> Startup ont développé des services très innovant : moteurs de recherches, blog, réseaux sociaux, plateformes...
>
> Tracking des utilisateurs =\> publicité ciblée =\> business model actuel
>
>  

## Maturité

>  
>
> Facteurs multiples contribuent à ce que le web évolue vers un web centralisé, contrôlé par une poignée de géants du numériques.
>
>  
>
>  
>
> Concentration commence à interroger voir à inquiéter :

- Fakenews, sensure

- Exploitation des données utilisateurs à leur insus

- Impact sur l'économie de ces géants : accaparation de la valeur ajoutée, soustraction à l'impot, position dominante

- Risque de souveraineté vis à vis des technologies américaine et demain chinoise

>  
>
> A faire
>
> A faire
>
> Iimportant
>
> Question
>
>  
>
>  
>
>  
>
> A [Website link](https://github.com/)
>
>  
>
>  
>
> docker container <span class="mark">**run** --name web -p \<hostPort\>:\<containerPort\></span> docker :3.9

- create and run the container from Alpine version 3.9 image, name the running container "web" and expose port 5000 externally mapped to port 80 inside the container

- Nb : donwoad image in local image cache from repo (download :latest by default), map ports, start container using the CMD in image Dockerfile

- Params

  - <span class="mark">-d --detach</span> : start the container in background

  - <span class="mark">--rm</span> : automatically delete the container when the container stops

  - <span class="mark">-it</span>: interactive : attach to the container

  - <span class="mark">-t</span> : allocate a pseudo TTY (console)

  - <span class="mark">-p</span> \<hostPort:containerPort\>

  - <span class="mark">-e</span> \<EnvVarName\>="\<EnvVarValue"\> : set env variable

  - <span class="mark">-v</span> \<host-src\>:\<container-dest\> : bind mount a volume

    - Nb: The 'host-src' is an absolute path or a name value

  <!-- -->

  - <span class="mark">--net\[work\]</span> \<NetworkName\> : attache container to a network

    - --net=host =\> use host network

  - <span class="mark">--net\[work\]-alias</span> list : add network alias (roundrobin between containers)

  - <span class="mark">--volumes-from</span> \<anotherContainerId\> : Mounts all the volumes from container ghost-site also to this temporary container. The mount points are the same as the original container.

- override the entrypoint by the bash :

  - docker run <span class="mark">-it</span> --rm myimage <span class="mark">bash</span>

  - Windows : --entrypoint "cmd.exe"

  - Linux : --entrypoint "bash"

>  
>
> docker run --rm -it -p 8000:80 -p 8001:443 -e ASPNETCORE_URLS="https://+;http://+" -e ASPNETCORE_HTTPS_PORT=8001 -e ASPNETCORE_ENVIRONMENT=Development -v %APPDATA%\microsoft\UserSecrets\\<span class="mark">/root/</span>.microsoft/usersecrets -v %USERPROFILE%\\aspnet\https:/root/.aspnet/https/ aspnetapp
>
>  
>
> docker container <span class="mark">exec -it</span> \<cid\> \<cmd\>

- Execute a command (bash) in a running container. Start an additional process in the container. Exit to end the process. The rest of the container processes continues to run

- Params

  - <span class="mark">--user</span> www-data : execute command with a specified user

  - docker container exec -it --user www-data nc_app_1 /bin/sh

>  
>
>  
>
>  
>
> <span class="mark">docker container **start**</span>

- Attach to an existing stopped container :

> docker container start -ai \<cid\>
>
> Options:
>
> -a, --attach Attach STDOUT/STDERR and forward signals
>
> --detach-keys string Override the key sequence for detaching a
>
> container
>
> -i, --interactive Attach container's STDIN
>
>  
>
>  
>
> <span class="mark">docker container **create **</span>

- create a container based on the specified image.

- docker create \<myimage\>

>  
>
> docker rm \<containerId\>

- remove a container

>  
>
> <span class="mark">docker container **restart**</span> OPTIONS\] CONTAINER \[CONTAINER...\]
>
>  
>
> <span class="mark">docker container **stop** web</span>

- Stop running container with SIGTERM

>  
>
> <span class="mark">docker container **kill** web</span>

- Stop running container with SIGKILL

>  
>
> docker network ls

- List networks

>  
>
> <span class="mark">docker container **ls** (docker ps )</span>

- List running containers

- Params

  - <span class="mark">-a</span> : List **all** containers, including stoped containers

  - <span class="mark">-f</span> : filter

    - docker container ls -f name=^/foo$

>  
>
>  
>
> <span class="mark">docker container **rm**</span>

- Params

  - -f : stop and remove the container

>  
>
> <span class="mark">docker container **logs** \<cid\></span>

- Get console output of the container

- Params

  - <span class="mark">--tail</span> \<Number\> : show X last lines

>  
>
> <span class="mark">docker container **top**</span> \<cid\>
>
>  
>
> <span class="mark">docker container **inspect**</span>

- Display detailed information on one or more containers

  - Environment variables

  - Network settings

  - Examples

    - docker container inspect --format '{{ .NetworkSettings.IPAddress }}' \<CID\>

>  
>
> <span class="mark">docker container **stats**</span>

- Display a live stream of container(s) resource usage statistics

  - Net I/O : network

  - Block I/O : disk

>  
>
> <span class="mark">docker container **port**</span>

- List port mappings or a specific mapping for the container

>  
>
>  
>
> Run command on **multiple containers**

- List only container ids : docker ps -aq

- docker stop <span class="mark">$(docker ps -aq)</span>

- docker rm $(docker ps -aq)

>  
>
>  
>
> Table example :
>
>  

<table>
<colgroup>
<col style="width: 51%" />
<col style="width: 31%" />
<col style="width: 17%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Company</strong></th>
<th><strong>Contact</strong></th>
<th><strong>Country</strong></th>
</tr>
</thead>
<tbody>
<tr class="odd">
<td>Alfreds Futterkiste</td>
<td>Maria Anders</td>
<td>Germany</td>
</tr>
<tr class="even">
<td>Centro comercial Moctezuma</td>
<td>Francisco Chang</td>
<td>Mexico</td>
</tr>
<tr class="odd">
<td>Ernst Handel</td>
<td>Roland Mendel</td>
<td>Austria</td>
</tr>
<tr class="even">
<td>Island Trading</td>
<td>Helen Bennett</td>
<td>UK</td>
</tr>
<tr class="odd">
<td>Laughing Bacchus Winecellars</td>
<td>Yoshi Tannamuri</td>
<td>Canada</td>
</tr>
<tr class="even">
<td>Magazzini Alimentari Riuniti</td>
<td>Giovanni Rovelli</td>
<td>Italy</td>
</tr>
</tbody>
</table>

>  
>
>  
>
> Should look like that image :
>
>  
>
> <img src="exp/commonmark/media/image2.png" style="width:3.95in;height:1.94167in" alt="Texte de remplacement généré par une machine : Company Alfre.$ Centro Moctezuma Ernst Handel Island Trading Laughing Bacchus Magazzinj Blunt&#39; Contact Maria Anders Francisco Chang Roland Mendel Helen Bennett Yoshi Giovanni Bpyel.lj Country Germany Mexico Austria UK Canada Italy " />
>
>  
>
>  

<table>
<colgroup>
<col style="width: 42%" />
<col style="width: 57%" />
</colgroup>
<thead>
<tr class="header">
<th><img src="exp/commonmark/media/image3.jpg" style="width:1.71667in;height:1.45833in" alt="panneau bois aggloméré" /></th>
<th><p>Aggloméré</p>
<ul>
<li><p>particules de bois, mélangées à une résine, qui sont collées entre elles par un pressage à chaud</p></li>
<li><p>bonne tenue dans le temps</p></li>
<li><p>pas utilisé a des fins décoratives</p></li>
<li><p>faible résistance à la flexion et a l'eclatement</p></li>
<li><p>intérieur uniquement</p></li>
<li><p>Nb : agglo hydrofugé pour pièce humides</p></li>
</ul></th>
</tr>
</thead>
<tbody>
<tr class="odd">
<td><p><img src="exp/commonmark/media/image4.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois mdf" /></p>
<p> </p></td>
<td><p>Medum (MDF)</p>
<ul>
<li><p>fibres de bois compressées et collées</p></li>
<li><p>texture lisse et nette qui facilite tous les types de finitions comme la peinture ou le vernis</p></li>
<li><p>coupes sont nettes et sans bavures</p></li>
<li><p>assez faible résistance à la flexion et à l’éclatement</p></li>
<li><p>hydrofugé pour cuisines, salles de bains, caves</p></li>
</ul></td>
</tr>
<tr class="even">
<td><p><img src="exp/commonmark/media/image5.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois osb" /></p>
<p> </p></td>
<td><p>Panneau OSB</p>
<ul>
<li><p>composés de différentes couches de longues lamelles de bois, collées entre elles, afin d'obtenir la même qualité que le bois massif.</p></li>
<li><p>aspect brut le réserve à des utilisations qui ne soient pas décoratives</p></li>
<li><p>bonne résistance à la flexion et à l'éclatement</p></li>
<li><p>types :</p>
<ul>
<li><p>l’OSB 2 : en milieu sec ;</p></li>
<li><p>l’OSB 3 : en milieu humide en intérieur (salle de bains, cuisine, cave...) ;</p></li>
<li><p>l’OSB 4 : à l'extérieur (sous abri).</p></li>
</ul></li>
</ul>
<blockquote>
<p> </p>
</blockquote></td>
</tr>
<tr class="odd">
<td><p><img src="exp/commonmark/media/image6.jpg" style="width:2.3in;height:1.95833in" alt="panneau contreplaqué" /></p>
<p> </p></td>
<td><p>Contreplaqué ordinaire</p>
<ul>
<li><p>constituée de feuilles de bois déroulées</p></li>
<li><p>bonne résistance à la torsion et à la charge</p></li>
<li><p>Epaisseurs</p>
<ul>
<li><p>relativement flexible jusqu'à 10 mm d'épaisseur</p></li>
<li><p>bonne résistance aux chocs à partir de 15 mm d'épaisseur</p></li>
<li><p>si carrelé épaisseur &gt; 20 mm</p></li>
</ul></li>
<li><p>Finitions : peinture, vernis, placage</p></li>
<li><p>usage intérieur dans des pièces sèches</p></li>
</ul>
<blockquote>
<p> </p>
</blockquote></td>
</tr>
<tr class="even">
<td><p><img src="exp/commonmark/media/image7.jpg" style="width:2.3in;height:2.3in" alt="panneau contreplaqué peuplier" /></p>
<p> </p></td>
<td><p>Contreplaqué peuplier</p>
<ul>
<li><p>léger mais sans perdre en solidité</p></li>
<li><p>peut convenir pour l’aménagement intérieur, ou la création de meubles bruts.</p></li>
</ul></td>
</tr>
<tr class="odd">
<td><p><img src="exp/commonmark/media/image8.jpg" style="width:2.3in;height:2.3in" alt="contreplaqué okumé exterieur" /></p>
<p> </p></td>
<td><p>contreplaqué okoumé extérieur</p>
<ul>
<li><p>traitement hydrofuge</p></li>
<li><p>peut être laissé à l’extérieur mais sous abri : pergola, demi-toit...</p></li>
</ul>
<p> </p>
<p>contreplaqué okoumé nautique (okoumé marin)</p>
<ul>
<li><p>bois hydrofuge qui supporte parfaitement bien les intempéries</p></li>
<li><p>peut être placé n’importe où dans le jardin</p></li>
</ul></td>
</tr>
<tr class="even">
<td><p><img src="exp/commonmark/media/image9.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite" /></p>
<p> </p></td>
<td><p>panneau en fibre composite</p>
<ul>
<li><p>Mélange de fibres de bois et de résine plastique</p></li>
<li><p>Totalement imputrescible</p></li>
<li><p>proposée dans différents coloris, sans besoin de finition supplémentaire</p></li>
</ul>
<blockquote>
<p> </p>
<p> </p>
<p> </p>
</blockquote></td>
</tr>
<tr class="odd">
<td><p><img src="exp/commonmark/media/image10.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite dure" /></p>
<p> </p></td>
<td><p>panneau en fibres dures</p>
<ul>
<li><p>fond de meubles</p></li>
</ul></td>
</tr>
<tr class="even">
<td><ul>
<li><p> </p></li>
</ul></td>
<td> </td>
</tr>
</tbody>
</table>

>  
>
>  
>
>  
