import { useEffect } from 'react';
import { SnackbarProvider, useSnackbar } from 'notistack';

let setErrorFunc = null
let setInfoFunc = null
let setSuccessFunc = null

export const setErrorMessage = message => setErrorFunc?.(message)
export const setInfoMessage = message => setInfoFunc?.(message)
export const setSuccessMessage = message => setSuccessFunc?.(message)

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
            })
        }
        setInfoFunc = msg => {
            const sb = enqueueSnackbar(msg, {
                variant: "info",
                onClick: () => closeSnackbar(sb),
            })
        }
        setSuccessFunc = msg => {
            console.log('Success snackbar')
            const sb = enqueueSnackbar(msg, {
                variant: "success",
                onClick: () => { console.log('close success'); closeSnackbar(sb) },
                autoHideDuration: 3000,
            })
        }
    }, [closeSnackbar, enqueueSnackbar])

    return null
}