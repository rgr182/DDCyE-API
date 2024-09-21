var selectedGender = null;
function selectGender(selectedButton) {
    var buttons = document.querySelectorAll('.gender');
    buttons.forEach(function (button) {
        button.classList.remove('selected');
    });
    selectedButton.classList.add('selected');
    selectedGender = selectedButton.textContent;
}

$(document).ready(function () {
    // Populate date dropdowns
    var months = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    var currentYear = new Date().getFullYear();

    for (var i = 1; i <= 31; i++) {
        $('#dayOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('dayDisplay', ${i})">${i}</a></li>`);
    }

    months.forEach(function (month, index) {
        $('#monthOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('monthDisplay', '${month}')">${month}</a></li>`);
    });

    for (var i = currentYear; i >= 1900; i--) {
        $('#yearOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('yearDisplay', ${i})">${i}</a></li>`);
    }

    var timeOutMessage;

    $(".RegisterButton").on("click", function () {
        var name = $("#name").val();
        var lastName = $("#lastName").val();
        var email = $('#email').val();
        var emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        var password = $('#Password').val();
        var confirmPassword = $('#ConfirmPassword').val();

        if (name === "") {
            showErrorMessage("Por favor, ingrese su nombre.");
            return;
        }
        if (lastName === "") {
            showErrorMessage("Por favor, ingrese su apellido.");
            return;
        }
        if (!email) {
            showErrorMessage("Por favor, ingrese su dirección de correo electrónico.");
            return;
        }
        if (!emailRegex.test(email)) {
            showErrorMessage("Por favor, ingrese una dirección de correo electrónico válida.");
            return;
        }
        if (password === "" || confirmPassword === "") {
            showErrorMessage("Favor de insertar su contraseña");
            return;
        }
        if (password !== confirmPassword) {
            showErrorMessage("Las contraseñas no coinciden.");
            return;
        }

        var day = $("#dayDisplay").text() !== "Día" ? $("#dayDisplay").text() : null;
        var month = $("#monthDisplay").text() !== "Mes" ? $("#monthDisplay").text() : null;
        var year = $("#yearDisplay").text() !== "Año" ? $("#yearDisplay").text() : null;

        var birthDate = null;
        if (day && month && year) {
            var monthNumber = months.indexOf(month) + 1;
            birthDate = `${year}-${monthNumber.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
        }

        var userData = {
            name: name,
            lastName: lastName,
            email: email,
            password: password
        };
        if (birthDate) {
            userData.birthDate = birthDate;
        }
        if (selectedGender) {
            userData.gender = selectedGender;
        }

        $.ajax({
            url: "/api/User/register",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(userData),
            success: (response) => {
                $("#successMessage").text("Registro realizado con éxito!").show();
                $('#errorMessage').hide();
            },
            error: function (xhr, status, error) {
                var errorMessage = xhr.responseJSON?.error || 'Error al hacer el registro de usuario';
                showErrorMessage(errorMessage);
            }
        });
    });

    function showErrorMessage(message) {
        $('#errorMessage').text(message).show();
        clearTimeout(timeOutMessage);
        timeOutMessage = setTimeout(() => {
            $('#errorMessage').hide();
        }, 5000);
    }
});

function selectDateOption(elementId, value) {
    document.getElementById(elementId).textContent = value;
}