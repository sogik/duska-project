#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "auth.h"
#include "basedatos.h"
#include <mysql.h>

int registrarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {

    if (usuarioExiste(conn, nombre_usuario) == 1) 
    {
        printf("El nombre de usuario ya está en uso.\n");
        return 1;
    }
    
    if (insertarUsuario(conn, nombre_usuario, contrasena)) 
    {
        return 0;
    } 
    else 
    {
        return 2;
    }
}

int iniciarSesion(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {

    if (usuarioExiste(conn, nombre_usuario) == 1) 
    {
        if (verificarCredenciales(conn, nombre_usuario, contrasena) == 0) 
        {
            return 0;
        } 
        else 
        {
            return 1;
        }
    } 
    else 
    {
        printf("El nombre de usuario no existe.\n");
        return 2;
    }
    
}