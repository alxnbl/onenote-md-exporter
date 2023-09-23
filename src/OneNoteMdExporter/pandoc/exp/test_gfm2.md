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
> ![Pin on GeoWeb](exp/gfm2/media/image1.jpg)
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
> docker container [**run** --name web -p \<hostPort\>:\<containerPort\>]{.mark} docker :3.9

- create and run the container from Alpine version 3.9 image, name the running container "web" and expose port 5000 externally mapped to port 80 inside the container

- Nb : donwoad image in local image cache from repo (download :latest by default), map ports, start container using the CMD in image Dockerfile

- Params

  - [-d --detach]{.mark} : start the container in background

  - [--rm]{.mark} : automatically delete the container when the container stops

  - [-it]{.mark}: interactive : attach to the container

  - [-t]{.mark} : allocate a pseudo TTY (console)

  - [-p]{.mark} \<hostPort:containerPort\>

  - [-e]{.mark} \<EnvVarName\>="\<EnvVarValue"\> : set env variable

  - [-v]{.mark} \<host-src\>:\<container-dest\> : bind mount a volume

    - Nb: The 'host-src' is an absolute path or a name value

  &nbsp;

  - [--net\[work\]]{.mark} \<NetworkName\> : attache container to a network

    - --net=host =\> use host network

  - [--net\[work\]-alias]{.mark} list : add network alias (roundrobin between containers)

  - [--volumes-from]{.mark} \<anotherContainerId\> : Mounts all the volumes from container ghost-site also to this temporary container. The mount points are the same as the original container.

- override the entrypoint by the bash :

  - docker run [-it]{.mark} --rm myimage [bash]{.mark}

  - Windows : --entrypoint "cmd.exe"

  - Linux : --entrypoint "bash"

>  
>
> docker run --rm -it -p 8000:80 -p 8001:443 -e ASPNETCORE_URLS="https://+;http://+" -e ASPNETCORE_HTTPS_PORT=8001 -e ASPNETCORE_ENVIRONMENT=Development -v %APPDATA%\microsoft\UserSecrets\\[/root/]{.mark}.microsoft/usersecrets -v %USERPROFILE%\\aspnet\https:/root/.aspnet/https/ aspnetapp
>
>  
>
> docker container [exec -it]{.mark} \<cid\> \<cmd\>

- Execute a command (bash) in a running container. Start an additional process in the container. Exit to end the process. The rest of the container processes continues to run

- Params

  - [--user]{.mark} www-data : execute command with a specified user

  - docker container exec -it --user www-data nc_app_1 /bin/sh

>  
>
>  
>
>  
>
> [docker container **start**]{.mark}

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
> [docker container **create **]{.mark}

- create a container based on the specified image.

- docker create \<myimage\>

>  
>
> docker rm \<containerId\>

- remove a container

>  
>
> [docker container **restart**]{.mark} OPTIONS\] CONTAINER \[CONTAINER...\]
>
>  
>
> [docker container **stop** web]{.mark}

- Stop running container with SIGTERM

>  
>
> [docker container **kill** web]{.mark}

- Stop running container with SIGKILL

>  
>
> docker network ls

- List networks

>  
>
> [docker container **ls** (docker ps )]{.mark}

- List running containers

- Params

  - [-a]{.mark} : List **all** containers, including stoped containers

  - [-f]{.mark} : filter

    - docker container ls -f name=^/foo\$

>  
>
>  
>
> [docker container **rm**]{.mark}

- Params

  - -f : stop and remove the container

>  
>
> [docker container **logs** \<cid\>]{.mark}

- Get console output of the container

- Params

  - [--tail]{.mark} \<Number\> : show X last lines

>  
>
> [docker container **top**]{.mark} \<cid\>
>
>  
>
> [docker container **inspect**]{.mark}

- Display detailed information on one or more containers

  - Environment variables

  - Network settings

  - Examples

    - docker container inspect --format '{{ .NetworkSettings.IPAddress }}' \<CID\>

>  
>
> [docker container **stats**]{.mark}

- Display a live stream of container(s) resource usage statistics

  - Net I/O : network

  - Block I/O : disk

>  
>
> [docker container **port**]{.mark}

- List port mappings or a specific mapping for the container

>  
>
>  
>
> Run command on **multiple containers**

- List only container ids : docker ps -aq

- docker stop [\$(docker ps -aq)]{.mark}

- docker rm \$(docker ps -aq)

>  
>
>  
>
> Table example :
>
>  

| **Company**                  | **Contact**      | **Country** |
|------------------------------|------------------|-------------|
| Alfreds Futterkiste          | Maria Anders     | Germany     |
| Centro comercial Moctezuma   | Francisco Chang  | Mexico      |
| Ernst Handel                 | Roland Mendel    | Austria     |
| Island Trading               | Helen Bennett    | UK          |
| Laughing Bacchus Winecellars | Yoshi Tannamuri  | Canada      |
| Magazzini Alimentari Riuniti | Giovanni Rovelli | Italy       |

>  
>
>  
>
> Should look like that image :
>
>  
>
> ![Texte de remplacement généré par une machine : Company Alfre.\$ Centro Moctezuma Ernst Handel Island Trading Laughing Bacchus Magazzinj Blunt' Contact Maria Anders Francisco Chang Roland Mendel Helen Bennett Yoshi Giovanni Bpyel.lj Country Germany Mexico Austria UK Canada Italy ](exp/gfm2/media/image2.png)
>
>  
>
>  

[TABLE]

>  
>
>  
>
>  
