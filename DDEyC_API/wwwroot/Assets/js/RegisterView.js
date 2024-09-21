var selectedGender = null;
const months = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

const ERROR_MESSAGES = {
    NAME_REQUIRED: "Por favor, ingrese su nombre.",
    LASTNAME_REQUIRED: "Por favor, ingrese su apellido.",
    EMAIL_REQUIRED: "Por favor, ingrese su dirección de correo electrónico.",
    EMAIL_INVALID: "Por favor, ingrese una dirección de correo electrónico válida.",
    PASSWORD_REQUIRED: "Favor de insertar su contraseña",
    PASSWORDS_MISMATCH: "Las contraseñas no coinciden.",
    INVALID_INPUT: "La contraseña debe de tener al menos 8 caracteres.",
    REGISTRATION_FAILED: "Error al hacer el registro de usuario",
    DEFAULT: "Ocurrió un error. Por favor, inténtelo de nuevo.",
    EMAIL_ALREADY_REGISTERED: "Ya existe una cuenta con esa dirección de correo electrónico."
};

function selectGender(selectedButton) {
    var buttons = document.querySelectorAll('.gender');
    buttons.forEach(function (button) {
        button.classList.remove('selected');
    });
    selectedButton.classList.add('selected');
    selectedGender = selectedButton.textContent;
}

function populateDateDropdowns() {
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
}

function selectDateOption(elementId, value) {
    document.getElementById(elementId).textContent = value;
}

function showErrorMessage(message) {
    $('#errorMessage').text(message).show();
    clearTimeout(window.timeOutMessage);
    window.timeOutMessage = setTimeout(() => {
        $('#errorMessage').hide();
    }, 5000);
}

function validateForm() {
    const name = $("#name").val().trim();
    const lastName = $("#lastName").val().trim();
    const email = $('#email').val().trim();
    const password = $('#Password').val();
    const confirmPassword = $('#ConfirmPassword').val();
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (name === "") {
        showErrorMessage(ERROR_MESSAGES.NAME_REQUIRED);
        return false;
    }
    if (lastName === "") {
        showErrorMessage(ERROR_MESSAGES.LASTNAME_REQUIRED);
        return false;
    }
    if (!email) {
        showErrorMessage(ERROR_MESSAGES.EMAIL_REQUIRED);
        return false;
    }
    if (!emailRegex.test(email)) {
        showErrorMessage(ERROR_MESSAGES.EMAIL_INVALID);
        return false;
    }
    if (password === "" || confirmPassword === "") {
        showErrorMessage(ERROR_MESSAGES.PASSWORD_REQUIRED);
        return false;
    }
    if (password !== confirmPassword) {
        showErrorMessage(ERROR_MESSAGES.PASSWORDS_MISMATCH);
        return false;
    }

    return true;
}

function getBirthDate() {
    const day = $("#dayDisplay").text() !== "Día" ? $("#dayDisplay").text() : null;
    const month = $("#monthDisplay").text() !== "Mes" ? $("#monthDisplay").text() : null;
    const year = $("#yearDisplay").text() !== "Año" ? $("#yearDisplay").text() : null;

    if (day && month && year) {
        const monthNumber = months.indexOf(month) + 1;
        return `${year}-${monthNumber.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
    }

    return null;
}

function registerUser(userData) {
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
            const errorMessage = xhr.responseJSON?.error || ERROR_MESSAGES.REGISTRATION_FAILED;
            showErrorMessage(errorMessage);
        }
    });
}

$(document).ready(function () {
    populateDateDropdowns();

    $(".RegisterButton").on("click", function () {
        if (!validateForm()) {
            return;
        }

        const userData = {
            name: $("#name").val().trim(),
            lastName: $("#lastName").val().trim(),
            email: $('#email').val().trim(),
            password: $('#Password').val()
        };

        const birthDate = getBirthDate();
        if (birthDate) {
            userData.birthDate = birthDate;
        }

        if (selectedGender) {
            userData.gender = selectedGender;
        }

        registerUser(userData);
    });
});