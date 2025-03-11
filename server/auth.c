#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <../libraries/bcrypt.h>
#include "auth.h"
#include "database.h"

void hashContrasena(const char* contrasena, char hash[BCRYPT_HASHSIZE]) {
    // Genera un salt automáticamente y hashea la contraseña
    if (bcrypt_hashpw(contrasena, bcrypt_gensalt(12), hash) != 0) 
    {
        printf("Error al hashear la contraseña.\n");
    }
}

int registrarUsuario(const char* nombre_usuario, const char* contrasena) {

    char hash[BCRYPT_HASHSIZE];
    hashContrasena(contrasena, hash);

    if (usuarioExiste(nombre_usuario)) 
    {
        printf("El nombre de usuario ya está en uso.\n");
        return 1;
    }
    
    if (insertarUsuario(nombre_usuario, hash)) 
    {
        return 0;
    } 
    else 
    {
        return 1;
    }
}

int iniciarSesion(const char* nombre_usuario, const char* contrasena) {

    char hash[BCRYPT_HASHSIZE];
    hashContrasena(contrasena, hash);

    if (verificarCredenciales(nombre_usuario, hash)) 
    {
        return 0;
    } 
    else 
    {
        return 1;
    }
}