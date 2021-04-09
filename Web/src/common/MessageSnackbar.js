import { useEffect } from 'react';
import { SnackbarProvider, useSnackbar } from 'notistack';

let setErrorFunc = null
export const setErrorMessage = message => setErrorFunc?.(message)

export const MessageProvider = (props) => {
    return (
        <SnackbarProvider
            maxSnack={3}
            preventDuplicate={true}
            anchorOrigin={{
                vertical: 'top',
                horizontal: 'center'
            }}>
            <Enable />
            {props.children}
        </SnackbarProvider>
    )
}

const Enable = () => {
    const { enqueueSnackbar, closeSnackbar } = useSnackbar();

    useEffect(() => {
        // Initialize canvas
        setErrorFunc = errorMsg => {
            const sb = enqueueSnackbar(errorMsg, {
                variant: "error",
                onClick: () => closeSnackbar(sb),
                autoHideDuration: null
            })
        }



    }, [closeSnackbar, enqueueSnackbar])

    return null
}