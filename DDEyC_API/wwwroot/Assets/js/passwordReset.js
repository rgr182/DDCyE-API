var timeOutMessage = setTimeout(() => {
    $('#errorMessage').hide();
}, 5000);

$(document).ready(() => {
    $('#back').on('click', function() {
        window.location.href = backButtonUrl;
    });

    $('#resetPasswordBtn').on('click', function () {
        var newPassword = $('#newPassword').val();
        var confirmPassword = $('#confirmPassword').val();
        var token = $('#token').val();
        
        if (newPassword === "" || confirmPassword === "") {
            $('#errorMessage').text("Favor de insertar su nueva contraseña").show();
            timeOutMessage
            return;
        }
        if (newPassword !== confirmPassword) {
            $('#errorMessage').text("Las contraseñas no coinciden.").show();
            timeOutMessage
            return;
        }
        
        $.ajax({
            url: '/api/auth/resetPassword',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                token: token,
                newPassword: newPassword
            }),
            success: function() {
                $('#resetPasswordForm').hide();
                $('#successMessage').show();
            },
            error: function(xhr) {
                let errorMessage;
                let httpStatus = xhr.status;
                
                if (!navigator.onLine || httpStatus === 0) {
                    errorMessage = 'No hay conexión con el servidor. Por favor, verifique su conexión a internet.';
                } else if (httpStatus === 401) {
                    errorMessage = 'El enlace de recuperación ha expirado. Por favor, solicite uno nuevo.';
                } else if (httpStatus === 400) {
                    errorMessage = 'La nueva contraseña no cumple con los requisitos de seguridad.';
                } else {
                    errorMessage = 'Ocurrió un error al restablecer la contraseña. Por favor, inténtelo de nuevo más tarde.';
                }

                $('#errorMessage').text(errorMessage).show();
                timeOutMessage;
            }
        });
    });
});