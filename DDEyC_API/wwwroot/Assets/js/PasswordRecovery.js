const LOGINPAGE = "https://localhost:44347/LoginPage";

var timeOutMessage = setTimeout(() => {
    $('#errorMessage').hide();
}, 5000);

// Script to send data with jQuery
$(document).ready(() => {
    $('#sendRecoveryLink').on('click', function () {
        var email = $('.email').val();
        var emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

        // Validate if the email input is empty
        if (!email) {
            $('#errorMessage').text("Por favor, ingrese su dirección de correo electrónico.").show();
            timeOutMessage
            return;
        }

        // Validate the email format
        if (!emailRegex.test(email)) {
            $('#errorMessage').text("Por favor, ingrese una dirección de correo electrónico válida.").show();
            timeOutMessage
            return;
        }

        // Send the AJAX request to the backend
        $.ajax({
            url: '/api/auth/passwordRecovery',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(email),
            success: () => {
                // Hide initial content and show success message
                $('.initialContent').fadeOut(400, function () {
                    $('.successContent').fadeIn(400);
                    $('#errorMessage').hide();
                });
            },
            error: function (xhr, status) {
                // Handle different error responses based on status code
                if (status === 0) {
                    $('#errorMessage').text("Estás desconectado. Verifica tu conexión e inténtalo de nuevo.").show();
                    timeOutMessage
                } else if (xhr.status === 404) {
                    $('#errorMessage').text("No se encontró una cuenta con esa dirección de correo electrónico.").show();
                    timeOutMessage
                } else if (xhr.status === 500) {
                    $('#errorMessage').text("Error del servidor. Inténtalo de nuevo más tarde.").show();
                    timeOutMessage
                } else if (xhr.status === 408) {
                    $('#errorMessage').text("El servidor no responde. Inténtalo de nuevo más tarde.").show();
                    timeOutMessage
                } else {
                    $('#errorMessage').text("Ocurrió un error. Inténtalo de nuevo.").show();
                    timeOutMessage
                }
                $('#successContent').hide();
            }
        });
    });

    $(".back").on("click", function () {
        window.location.href = LOGINPAGE;
    });
});