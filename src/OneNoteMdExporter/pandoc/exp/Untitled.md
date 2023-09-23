---

title: Long text

updated: 2023-09-23T14:39:40.0000000+02:00

created: 2020-11-10T08:54:01.0000000+01:00

---

  

**Étape 1** – Établir des bases communes entre vous, votre auditoire et le projet.

  

# Evolution d'internet

  

## Genèse

  

![image1](../../resources/728e56705daa4ee280dd07b22998f393.jpg)

  

Début d'Internet : scientifiques puis geek

  

Décentralisé, technologie pas matures qui permettaient de faire des choses très basiques

  

HTTP et MAIL

  

## Evolution

  

Technologies ont gagnées en maturité

  

Startup ont développé des services très innovant : moteurs de recherches, blog, réseaux sociaux, plateformes...

  

Tracking des utilisateurs =\> publicité ciblée =\> business model actuel

  

## Maturité

  

Facteurs multiples contribuent à ce que le web évolue vers un web centralisé, contrôlé par une poignée de géants du numériques.

  

Concentration commence à interroger voir à inquiéter :

- Fakenews, sensure

- Exploitation des données utilisateurs à leur insus

- Impact sur l'économie de ces géants : accaparation de la valeur ajoutée, soustraction à l'impot, position dominante

- Risque de souveraineté vis à vis des technologies américaine et demain chinoise

  

A faire

  

A faire

  

Iimportant

  

Question

  

A [Website link](https://github.com/)

  

docker container ==**run** --name web -p \<hostPort\>:\<containerPort\>== docker :3.9

- create and run the container from Alpine version 3.9 image, name the running container "web" and expose port 5000 externally mapped to port 80 inside the container

- Nb : donwoad image in local image cache from repo (download :latest by default), map ports, start container using the CMD in image Dockerfile

- Params

  - ==-d --detach== : start the container in background

  - ==--rm== : automatically delete the container when the container stops

  - ==-it==: interactive : attach to the container

  - ==-t== : allocate a pseudo TTY (console)

  - ==-p== \<hostPort:containerPort\>

  - ==-e== \<EnvVarName\>="\<EnvVarValue"\> : set env variable

  - ==-v== \<host-src\>:\<container-dest\> : bind mount a volume

    - Nb: The 'host-src' is an absolute path or a name value

  - ==--net\[work\]== \<NetworkName\> : attache container to a network

    - --net=host =\> use host network

  - ==--net\[work\]-alias== list : add network alias (roundrobin between containers)

  - ==--volumes-from== \<anotherContainerId\> : Mounts all the volumes from container ghost-site also to this temporary container. The mount points are the same as the original container.

- override the entrypoint by the bash :

  - docker run ==-it== --rm myimage ==bash==

  - Windows : --entrypoint "cmd.exe"

  - Linux : --entrypoint "bash"

  

docker run --rm -it -p 8000:80 -p 8001:443 -e ASPNETCORE_URLS="https://+;http://+" -e ASPNETCORE_HTTPS_PORT=8001 -e ASPNETCORE_ENVIRONMENT=Development -v %APPDATA%\microsoft\UserSecrets\\==/root/==.microsoft/usersecrets -v %USERPROFILE%\\aspnet\https:/root/.aspnet/https/ aspnetapp

  

docker container ==exec -it== \<cid\> \<cmd\>

- Execute a command (bash) in a running container. Start an additional process in the container. Exit to end the process. The rest of the container processes continues to run

- Params

  - ==--user== www-data : execute command with a specified user

  - docker container exec -it --user www-data nc_app_1 /bin/sh

  

==docker container **start**==

- Attach to an existing stopped container :

docker container start -ai \<cid\>

  

Options:

  

-a, --attach Attach STDOUT/STDERR and forward signals

  

--detach-keys string Override the key sequence for detaching a

  

container

  

-i, --interactive Attach container's STDIN

  

==docker container **create **==

- create a container based on the specified image.

- docker create \<myimage\>

  

docker rm \<containerId\>

- remove a container

  

==docker container **restart**== OPTIONS\] CONTAINER \[CONTAINER...\]

  

==docker container **stop** web==

- Stop running container with SIGTERM

  

==docker container **kill** web==

- Stop running container with SIGKILL

  

docker network ls

- List networks

  

==docker container **ls** (docker ps )==

- List running containers

- Params

  - ==-a== : List **all** containers, including stoped containers

  - ==-f== : filter

    - docker container ls -f name=^/foo\$

  

==docker container **rm**==

- Params

  - -f : stop and remove the container

  

==docker container **logs** \<cid\>==

- Get console output of the container

- Params

  - ==--tail== \<Number\> : show X last lines

  

==docker container **top**== \<cid\>

  

==docker container **inspect**==

- Display detailed information on one or more containers

  - Environment variables

  - Network settings

  - Examples

    - docker container inspect --format '{{ .NetworkSettings.IPAddress }}' \<CID\>

  

==docker container **stats**==

- Display a live stream of container(s) resource usage statistics

  - Net I/O : network

  - Block I/O : disk

  

==docker container **port**==

- List port mappings or a specific mapping for the container

  

Run command on **multiple containers**

- List only container ids : docker ps -aq

- docker stop ==\$(docker ps -aq)==

- docker rm \$(docker ps -aq)

  

Table example :

  

| **Company**                  | **Contact**      | **Country** |

|------------------------------|------------------|-------------|

| Alfreds Futterkiste          | Maria Anders     | Germany     |

| Centro comercial Moctezuma   | Francisco Chang  | Mexico      |

| Ernst Handel                 | Roland Mendel    | Austria     |

| Island Trading               | Helen Bennett    | UK          |

| Laughing Bacchus Winecellars | Yoshi Tannamuri  | Canada      |

| Magazzini Alimentari Riuniti | Giovanni Rovelli | Italy       |

  

Should look like that image :

  

![image2](../../resources/ac2362a09da34bea92247f68fe28dc56.png)

  

<table>

<colgroup>

<col style="width: 42%" />

<col style="width: 57%" />

</colgroup>

<thead>

<tr class="header">

<th>![image3](../../resources/48488132863c4cce918ed9fa30dfc63b.jpg)</th>

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

<td><p>![image4](../../resources/eeffa1e62bfc42fe964c704cfa2156ce.jpg)</p>

<p></p></td>

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

<td><p>![image5](../../resources/3baa4e1c2d014089be504bafb7aed339.jpg)</p>

<p></p></td>

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

<p></p>

</blockquote></td>

</tr>

<tr class="odd">

<td><p>![image6](../../resources/226718401a85485caf68f8e2cb54e405.jpg)</p>

<p></p></td>

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

<p></p>

</blockquote></td>

</tr>

<tr class="even">

<td><p>![image7](../../resources/ba5efc2058254ce2a1a0270309082128.jpg)</p>

<p></p></td>

<td><p>Contreplaqué peuplier</p>

<ul>

<li><p>léger mais sans perdre en solidité</p></li>

<li><p>peut convenir pour l’aménagement intérieur, ou la création de meubles bruts.</p></li>

</ul></td>

</tr>

<tr class="odd">

<td><p>![image8](../../resources/c271d33741fd41c58e8f3282c6639571.jpg)</p>

<p></p></td>

<td><p>contreplaqué okoumé extérieur</p>

<ul>

<li><p>traitement hydrofuge</p></li>

<li><p>peut être laissé à l’extérieur mais sous abri : pergola, demi-toit...</p></li>

</ul>

<p></p>

<p>contreplaqué okoumé nautique (okoumé marin)</p>

<ul>

<li><p>bois hydrofuge qui supporte parfaitement bien les intempéries</p></li>

<li><p>peut être placé n’importe où dans le jardin</p></li>

</ul></td>

</tr>

<tr class="even">

<td><p>![image9](../../resources/a001859875204027acdcf8e3c6b252fb.jpg)</p>

<p></p></td>

<td><p>panneau en fibre composite</p>

<ul>

<li><p>Mélange de fibres de bois et de résine plastique</p></li>

<li><p>Totalement imputrescible</p></li>

<li><p>proposée dans différents coloris, sans besoin de finition supplémentaire</p></li>

</ul>

<blockquote>

<p></p>

<p></p>

<p></p>

</blockquote></td>

</tr>

<tr class="odd">

<td><p>![image10](../../resources/16d8dde5b1214e8ba08f88585c88fe9d.jpg)</p>

<p></p></td>

<td><p>panneau en fibres dures</p>

<ul>

<li><p>fond de meubles</p></li>

</ul></td>

</tr>

<tr class="even">

<td><ul>

<li><p></p></li>

</ul></td>

<td></td>

</tr>

</tbody>

</table>