#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <mysql.h>
#include <auth.h>
#include <basedatos.h>

int main(int argc, char *argv[])
{
    int sock_conn, sock_listen, ret;
    struct sockaddr_in serv_adr;
    char peticion[512];
    char respuesta[512];

    if((sock_listen = socket(AF_INET, SOCK_STREAM, 0)) < 0)
        printf("Error creant socket");

    memset(&serv_adr, 0, sizeof(serv_adr));
    serv_adr.sin_family = AF_INET;

    serv_adr.sin_addr.s_addr = htonl(INADDR_ANY);

    serv_adr.sin_port = htons(9050);

    if(bind(sock_listen, (struct sockdaddr *) &serv_adr, sizeof(serv_adr)) < 0)
        printf("Error al bind");

    if(listen(sock_listen, 3) < 0)
    printf("Error en el listen");


    printf("Eschuchando\n");

    while(1)
    {
        sock_conn = accept(sock_listen, NULL, NULL);
        printf("He recibido conexion\n");

        ret=read(sock_conn, peticion, sizeof(peticion));
        printf("Recibido\n");

        peticion[ret]='\0';

        printf("Peticion: %s\n",peticion);

        conexion_base_datos();

        char *p = strtok(peticion, "/");
        int codigo =  atoi(p);
        p = strtok(NULL, "/");
        char usuario[50];
        strcpy(usuario, p);
        p = strtok(NULL, "/");
        char contrasena[72];

        if (codigo == 0)
        {
            registrarUsuario(usuario, contrasena);
        }
        else if (codigo == 1)
        {
            iniciarSesion(usuario, contrasena);
        }
        else if (codigo == 2)
        {
            listarJugadores();
        }
        else if (codigo == 3)
        {
            listarPartidas();
        }
        else
        {
            listarPartidasGanadas(usuario);    
        }

        while(p!=NULL)
        {   
            p = strtok (NULL, "/");
        }
        respuesta [strlen (respuesta) - 1] = '\0';

        printf("Resultado: %s\n", respuesta);
        write(sock_conn, respuesta, strlen(respuesta));

        desconectar_base_datos();
        close(sock_conn);
        return 0;
    }
}