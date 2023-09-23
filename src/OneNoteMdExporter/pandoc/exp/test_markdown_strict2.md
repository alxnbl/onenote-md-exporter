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
> <img src="exp/markdown_strict2/media/image1.jpg" style="width:6.24167in;height:3.33333in" alt="Pin on GeoWeb" />
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
> Tracking des utilisateurs =&gt; publicité ciblée =&gt; business model actuel
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

-   Fakenews, sensure

-   Exploitation des données utilisateurs à leur insus

-   Impact sur l'économie de ces géants : accaparation de la valeur ajoutée, soustraction à l'impot, position dominante

-   Risque de souveraineté vis à vis des technologies américaine et demain chinoise

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
> docker container <span class="mark">**run** --name web -p &lt;hostPort&gt;:&lt;containerPort&gt;</span> docker :3.9

-   create and run the container from Alpine version 3.9 image, name the running container "web" and expose port 5000 externally mapped to port 80 inside the container

-   Nb : donwoad image in local image cache from repo (download :latest by default), map ports, start container using the CMD in image Dockerfile

-   Params

    -   <span class="mark">-d --detach</span> : start the container in background

    -   <span class="mark">--rm</span> : automatically delete the container when the container stops

    -   <span class="mark">-it</span>: interactive : attach to the container

    -   <span class="mark">-t</span> : allocate a pseudo TTY (console)

    -   <span class="mark">-p</span> &lt;hostPort:containerPort&gt;

    -   <span class="mark">-e</span> &lt;EnvVarName&gt;="&lt;EnvVarValue"&gt; : set env variable

    -   <span class="mark">-v</span> &lt;host-src&gt;:&lt;container-dest&gt; : bind mount a volume

        -   Nb: The 'host-src' is an absolute path or a name value

    <!-- -->

    -   <span class="mark">--net\[work\]</span> &lt;NetworkName&gt; : attache container to a network

        -   --net=host =&gt; use host network

    -   <span class="mark">--net\[work\]-alias</span> list : add network alias (roundrobin between containers)

    -   <span class="mark">--volumes-from</span> &lt;anotherContainerId&gt; : Mounts all the volumes from container ghost-site also to this temporary container. The mount points are the same as the original container.

-   override the entrypoint by the bash :

    -   docker run <span class="mark">-it</span> --rm myimage <span class="mark">bash</span>

    -   Windows : --entrypoint "cmd.exe"

    -   Linux : --entrypoint "bash"

>  
>
> docker run --rm -it -p 8000:80 -p 8001:443 -e ASPNETCORE\_URLS="https://+;http://+" -e ASPNETCORE\_HTTPS\_PORT=8001 -e ASPNETCORE\_ENVIRONMENT=Development -v %APPDATA%\microsoft\UserSecrets\\<span class="mark">/root/</span>.microsoft/usersecrets -v %USERPROFILE%\\aspnet\https:/root/.aspnet/https/ aspnetapp
>
>  
>
> docker container <span class="mark">exec -it</span> &lt;cid&gt; &lt;cmd&gt;

-   Execute a command (bash) in a running container. Start an additional process in the container. Exit to end the process. The rest of the container processes continues to run

-   Params

    -   <span class="mark">--user</span> www-data : execute command with a specified user

    -   docker container exec -it --user www-data nc\_app\_1 /bin/sh

>  
>
>  
>
>  
>
> <span class="mark">docker container **start**</span>

-   Attach to an existing stopped container :

> docker container start -ai &lt;cid&gt;
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

-   create a container based on the specified image.

-   docker create &lt;myimage&gt;

>  
>
> docker rm &lt;containerId&gt;

-   remove a container

>  
>
> <span class="mark">docker container **restart**</span> OPTIONS\] CONTAINER \[CONTAINER...\]
>
>  
>
> <span class="mark">docker container **stop** web</span>

-   Stop running container with SIGTERM

>  
>
> <span class="mark">docker container **kill** web</span>

-   Stop running container with SIGKILL

>  
>
> docker network ls

-   List networks

>  
>
> <span class="mark">docker container **ls** (docker ps )</span>

-   List running containers

-   Params

    -   <span class="mark">-a</span> : List **all** containers, including stoped containers

    -   <span class="mark">-f</span> : filter

        -   docker container ls -f name=^/foo$

>  
>
>  
>
> <span class="mark">docker container **rm**</span>

-   Params

    -   -f : stop and remove the container

>  
>
> <span class="mark">docker container **logs** &lt;cid&gt;</span>

-   Get console output of the container

-   Params

    -   <span class="mark">--tail</span> &lt;Number&gt; : show X last lines

>  
>
> <span class="mark">docker container **top**</span> &lt;cid&gt;
>
>  
>
> <span class="mark">docker container **inspect**</span>

-   Display detailed information on one or more containers

    -   Environment variables

    -   Network settings

    -   Examples

        -   docker container inspect --format '{{ .NetworkSettings.IPAddress }}' &lt;CID&gt;

>  
>
> <span class="mark">docker container **stats**</span>

-   Display a live stream of container(s) resource usage statistics

    -   Net I/O : network

    -   Block I/O : disk

>  
>
> <span class="mark">docker container **port**</span>

-   List port mappings or a specific mapping for the container

>  
>
>  
>
> Run command on **multiple containers**

-   List only container ids : docker ps -aq

-   docker stop <span class="mark">$(docker ps -aq)</span>

-   docker rm $(docker ps -aq)

>  
>
>  
>
> Table example :
>
>  

  -------------------------------------------------------------------------
  **Company**                          **Contact**            **Country**
  ------------------------------------ ---------------------- -------------
  Alfreds Futterkiste                  Maria Anders           Germany

  Centro comercial Moctezuma           Francisco Chang        Mexico

  Ernst Handel                         Roland Mendel          Austria

  Island Trading                       Helen Bennett          UK

  Laughing Bacchus Winecellars         Yoshi Tannamuri        Canada

  Magazzini Alimentari Riuniti         Giovanni Rovelli       Italy
  -------------------------------------------------------------------------

>  
>
>  
>
> Should look like that image :
>
>  
>
> <img src="exp/markdown_strict2/media/image2.png" style="width:3.95in;height:1.94167in" alt="Texte de remplacement généré par une machine : Company Alfre.$ Centro Moctezuma Ernst Handel Island Trading Laughing Bacchus Magazzinj Blunt&#39; Contact Maria Anders Francisco Chang Roland Mendel Helen Bennett Yoshi Giovanni Bpyel.lj Country Germany Mexico Austria UK Canada Italy " />
>
>  
>
>  

+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image3.jpg" style="width:1.71667in;height:1.45833in" alt="panneau bois aggloméré" />    | Aggloméré                                                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   particules de bois, mélangées à une résine, qui sont collées entre elles par un pressage à chaud                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne tenue dans le temps                                                                                                            |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   pas utilisé a des fins décoratives                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   faible résistance à la flexion et a l'eclatement                                                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   intérieur uniquement                                                                                                                 |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Nb : agglo hydrofugé pour pièce humides                                                                                              |
+==============================================================================================================================+==========================================================================================================================================+
| <img src="exp/markdown_strict2/media/image4.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois mdf" />              | Medum (MDF)                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   fibres de bois compressées et collées                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   texture lisse et nette qui facilite tous les types de finitions comme la peinture ou le vernis                                       |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   coupes sont nettes et sans bavures                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   assez faible résistance à la flexion et à l’éclatement                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   hydrofugé pour cuisines, salles de bains, caves                                                                                      |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image5.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois osb" />              | Panneau OSB                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   composés de différentes couches de longues lamelles de bois, collées entre elles, afin d'obtenir la même qualité que le bois massif. |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   aspect brut le réserve à des utilisations qui ne soient pas décoratives                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne résistance à la flexion et à l'éclatement                                                                                      |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   types :                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 2 : en milieu sec ;                                                                                                        |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 3 : en milieu humide en intérieur (salle de bains, cuisine, cave...) ;                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 4 : à l'extérieur (sous abri).                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image6.jpg" style="width:2.3in;height:1.95833in" alt="panneau contreplaqué" />          | Contreplaqué ordinaire                                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   constituée de feuilles de bois déroulées                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne résistance à la torsion et à la charge                                                                                         |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Epaisseurs                                                                                                                           |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   relativement flexible jusqu'à 10 mm d'épaisseur                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   bonne résistance aux chocs à partir de 15 mm d'épaisseur                                                                         |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   si carrelé épaisseur &gt; 20 mm                                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Finitions : peinture, vernis, placage                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   usage intérieur dans des pièces sèches                                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image7.jpg" style="width:2.3in;height:2.3in" alt="panneau contreplaqué peuplier" />     | Contreplaqué peuplier                                                                                                                    |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   léger mais sans perdre en solidité                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut convenir pour l’aménagement intérieur, ou la création de meubles bruts.                                                         |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image8.jpg" style="width:2.3in;height:2.3in" alt="contreplaqué okumé exterieur" />      | contreplaqué okoumé extérieur                                                                                                            |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   traitement hydrofuge                                                                                                                 |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut être laissé à l’extérieur mais sous abri : pergola, demi-toit...                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | contreplaqué okoumé nautique (okoumé marin)                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bois hydrofuge qui supporte parfaitement bien les intempéries                                                                        |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut être placé n’importe où dans le jardin                                                                                          |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image9.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite" />       | panneau en fibre composite                                                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Mélange de fibres de bois et de résine plastique                                                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Totalement imputrescible                                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   proposée dans différents coloris, sans besoin de finition supplémentaire                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image10.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite dure" /> | panneau en fibres dures                                                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   fond de meubles                                                                                                                      |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| -                                                                                                                            |                                                                                                                                          |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+

>  
>
>  
>
>  

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
> <img src="exp/markdown_strict2/media/image1.jpg" style="width:6.24167in;height:3.33333in" alt="Pin on GeoWeb" />
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
> Tracking des utilisateurs =&gt; publicité ciblée =&gt; business model actuel
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

-   Fakenews, sensure

-   Exploitation des données utilisateurs à leur insus

-   Impact sur l'économie de ces géants : accaparation de la valeur ajoutée, soustraction à l'impot, position dominante

-   Risque de souveraineté vis à vis des technologies américaine et demain chinoise

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
> docker container <span class="mark">**run** --name web -p &lt;hostPort&gt;:&lt;containerPort&gt;</span> docker :3.9

-   create and run the container from Alpine version 3.9 image, name the running container "web" and expose port 5000 externally mapped to port 80 inside the container

-   Nb : donwoad image in local image cache from repo (download :latest by default), map ports, start container using the CMD in image Dockerfile

-   Params

    -   <span class="mark">-d --detach</span> : start the container in background

    -   <span class="mark">--rm</span> : automatically delete the container when the container stops

    -   <span class="mark">-it</span>: interactive : attach to the container

    -   <span class="mark">-t</span> : allocate a pseudo TTY (console)

    -   <span class="mark">-p</span> &lt;hostPort:containerPort&gt;

    -   <span class="mark">-e</span> &lt;EnvVarName&gt;="&lt;EnvVarValue"&gt; : set env variable

    -   <span class="mark">-v</span> &lt;host-src&gt;:&lt;container-dest&gt; : bind mount a volume

        -   Nb: The 'host-src' is an absolute path or a name value

    <!-- -->

    -   <span class="mark">--net\[work\]</span> &lt;NetworkName&gt; : attache container to a network

        -   --net=host =&gt; use host network

    -   <span class="mark">--net\[work\]-alias</span> list : add network alias (roundrobin between containers)

    -   <span class="mark">--volumes-from</span> &lt;anotherContainerId&gt; : Mounts all the volumes from container ghost-site also to this temporary container. The mount points are the same as the original container.

-   override the entrypoint by the bash :

    -   docker run <span class="mark">-it</span> --rm myimage <span class="mark">bash</span>

    -   Windows : --entrypoint "cmd.exe"

    -   Linux : --entrypoint "bash"

>  
>
> docker run --rm -it -p 8000:80 -p 8001:443 -e ASPNETCORE\_URLS="https://+;http://+" -e ASPNETCORE\_HTTPS\_PORT=8001 -e ASPNETCORE\_ENVIRONMENT=Development -v %APPDATA%\microsoft\UserSecrets\\<span class="mark">/root/</span>.microsoft/usersecrets -v %USERPROFILE%\\aspnet\https:/root/.aspnet/https/ aspnetapp
>
>  
>
> docker container <span class="mark">exec -it</span> &lt;cid&gt; &lt;cmd&gt;

-   Execute a command (bash) in a running container. Start an additional process in the container. Exit to end the process. The rest of the container processes continues to run

-   Params

    -   <span class="mark">--user</span> www-data : execute command with a specified user

    -   docker container exec -it --user www-data nc\_app\_1 /bin/sh

>  
>
>  
>
>  
>
> <span class="mark">docker container **start**</span>

-   Attach to an existing stopped container :

> docker container start -ai &lt;cid&gt;
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

-   create a container based on the specified image.

-   docker create &lt;myimage&gt;

>  
>
> docker rm &lt;containerId&gt;

-   remove a container

>  
>
> <span class="mark">docker container **restart**</span> OPTIONS\] CONTAINER \[CONTAINER...\]
>
>  
>
> <span class="mark">docker container **stop** web</span>

-   Stop running container with SIGTERM

>  
>
> <span class="mark">docker container **kill** web</span>

-   Stop running container with SIGKILL

>  
>
> docker network ls

-   List networks

>  
>
> <span class="mark">docker container **ls** (docker ps )</span>

-   List running containers

-   Params

    -   <span class="mark">-a</span> : List **all** containers, including stoped containers

    -   <span class="mark">-f</span> : filter

        -   docker container ls -f name=^/foo$

>  
>
>  
>
> <span class="mark">docker container **rm**</span>

-   Params

    -   -f : stop and remove the container

>  
>
> <span class="mark">docker container **logs** &lt;cid&gt;</span>

-   Get console output of the container

-   Params

    -   <span class="mark">--tail</span> &lt;Number&gt; : show X last lines

>  
>
> <span class="mark">docker container **top**</span> &lt;cid&gt;
>
>  
>
> <span class="mark">docker container **inspect**</span>

-   Display detailed information on one or more containers

    -   Environment variables

    -   Network settings

    -   Examples

        -   docker container inspect --format '{{ .NetworkSettings.IPAddress }}' &lt;CID&gt;

>  
>
> <span class="mark">docker container **stats**</span>

-   Display a live stream of container(s) resource usage statistics

    -   Net I/O : network

    -   Block I/O : disk

>  
>
> <span class="mark">docker container **port**</span>

-   List port mappings or a specific mapping for the container

>  
>
>  
>
> Run command on **multiple containers**

-   List only container ids : docker ps -aq

-   docker stop <span class="mark">$(docker ps -aq)</span>

-   docker rm $(docker ps -aq)

>  
>
>  
>
> Table example :
>
>  

  -------------------------------------------------------------------------
  **Company**                          **Contact**            **Country**
  ------------------------------------ ---------------------- -------------
  Alfreds Futterkiste                  Maria Anders           Germany

  Centro comercial Moctezuma           Francisco Chang        Mexico

  Ernst Handel                         Roland Mendel          Austria

  Island Trading                       Helen Bennett          UK

  Laughing Bacchus Winecellars         Yoshi Tannamuri        Canada

  Magazzini Alimentari Riuniti         Giovanni Rovelli       Italy
  -------------------------------------------------------------------------

>  
>
>  
>
> Should look like that image :
>
>  
>
> <img src="exp/markdown_strict2/media/image2.png" style="width:3.95in;height:1.94167in" alt="Texte de remplacement généré par une machine : Company Alfre.$ Centro Moctezuma Ernst Handel Island Trading Laughing Bacchus Magazzinj Blunt&#39; Contact Maria Anders Francisco Chang Roland Mendel Helen Bennett Yoshi Giovanni Bpyel.lj Country Germany Mexico Austria UK Canada Italy " />
>
>  
>
>  

+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image3.jpg" style="width:1.71667in;height:1.45833in" alt="panneau bois aggloméré" />    | Aggloméré                                                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   particules de bois, mélangées à une résine, qui sont collées entre elles par un pressage à chaud                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne tenue dans le temps                                                                                                            |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   pas utilisé a des fins décoratives                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   faible résistance à la flexion et a l'eclatement                                                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   intérieur uniquement                                                                                                                 |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Nb : agglo hydrofugé pour pièce humides                                                                                              |
+==============================================================================================================================+==========================================================================================================================================+
| <img src="exp/markdown_strict2/media/image4.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois mdf" />              | Medum (MDF)                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   fibres de bois compressées et collées                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   texture lisse et nette qui facilite tous les types de finitions comme la peinture ou le vernis                                       |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   coupes sont nettes et sans bavures                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   assez faible résistance à la flexion et à l’éclatement                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   hydrofugé pour cuisines, salles de bains, caves                                                                                      |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image5.jpg" style="width:2.3in;height:1.95833in" alt="panneau bois osb" />              | Panneau OSB                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   composés de différentes couches de longues lamelles de bois, collées entre elles, afin d'obtenir la même qualité que le bois massif. |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   aspect brut le réserve à des utilisations qui ne soient pas décoratives                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne résistance à la flexion et à l'éclatement                                                                                      |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   types :                                                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 2 : en milieu sec ;                                                                                                        |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 3 : en milieu humide en intérieur (salle de bains, cuisine, cave...) ;                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   l’OSB 4 : à l'extérieur (sous abri).                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image6.jpg" style="width:2.3in;height:1.95833in" alt="panneau contreplaqué" />          | Contreplaqué ordinaire                                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   constituée de feuilles de bois déroulées                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bonne résistance à la torsion et à la charge                                                                                         |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Epaisseurs                                                                                                                           |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   relativement flexible jusqu'à 10 mm d'épaisseur                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   bonne résistance aux chocs à partir de 15 mm d'épaisseur                                                                         |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |     -   si carrelé épaisseur &gt; 20 mm                                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Finitions : peinture, vernis, placage                                                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   usage intérieur dans des pièces sèches                                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image7.jpg" style="width:2.3in;height:2.3in" alt="panneau contreplaqué peuplier" />     | Contreplaqué peuplier                                                                                                                    |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   léger mais sans perdre en solidité                                                                                                   |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut convenir pour l’aménagement intérieur, ou la création de meubles bruts.                                                         |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image8.jpg" style="width:2.3in;height:2.3in" alt="contreplaqué okumé exterieur" />      | contreplaqué okoumé extérieur                                                                                                            |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   traitement hydrofuge                                                                                                                 |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut être laissé à l’extérieur mais sous abri : pergola, demi-toit...                                                                |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | contreplaqué okoumé nautique (okoumé marin)                                                                                              |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   bois hydrofuge qui supporte parfaitement bien les intempéries                                                                        |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   peut être placé n’importe où dans le jardin                                                                                          |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image9.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite" />       | panneau en fibre composite                                                                                                               |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Mélange de fibres de bois et de résine plastique                                                                                     |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   Totalement imputrescible                                                                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   proposée dans différents coloris, sans besoin de finition supplémentaire                                                             |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
|                                                                                                                              | >                                                                                                                                        |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| <img src="exp/markdown_strict2/media/image10.jpg" style="width:2.3in;height:1.95833in" alt="panneau fibre composite dure" /> | panneau en fibres dures                                                                                                                  |
|                                                                                                                              |                                                                                                                                          |
|                                                                                                                              | -   fond de meubles                                                                                                                      |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+
| -                                                                                                                            |                                                                                                                                          |
+------------------------------------------------------------------------------------------------------------------------------+------------------------------------------------------------------------------------------------------------------------------------------+

>  
>
>  
>
>  
