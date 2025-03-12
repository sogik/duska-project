#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "auth.h"
#include "basedatos.h"

int registrarUsuario(const char* nombre_usuario, const char* contrasena) {

    if (usuarioExiste(nombre_usuario)) 
    {
        printf("El nombre de usuario ya está en uso.\n");
        return 1;
    }
    
    if (insertarUsuario(nombre_usuario, contrasena)) 
    {
        return 0;
    } 
    else 
    {
        return 1;
    }
}

int iniciarSesion(const char* nombre_usuario, const char* contrasena) {

    if (verificarCredenciales(nombre_usuario, contrasena)) 
    {
        return 0;
    } 
    else 
    {
        return 1;
    }
}