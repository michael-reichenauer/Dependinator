import { useState } from "react";
import { atom, useAtom } from "jotai"
import { Box, Button, Dialog, FormControlLabel, LinearProgress, Switch, Typography } from "@material-ui/core";
import { Formik, Form, Field } from 'formik';
import { TextField } from 'formik-material-ui';
import { sha256Hash } from '../common/utils'
import { store } from "./diagram/Store";
import { isDeveloperMode } from '../common/utils'


const loginAtom = atom(false)
const usernameKey = 'credential.userName'

export const useLogin = () => useAtom(loginAtom)


export default function Login() {
    const [show, setShow] = useLogin()
    const [createAccount, setCreateAccount] = useState(false)

    const loginWith = (provider) => {
        setShow(false)
        store.login(provider)
    }

    const handleEnter = (event) => {
        if (event.code === 'Enter') {
            const okButton = document.getElementById('OKButton')
            okButton.click()
        }
    }

    return (
        <Dialog open={show} onClose={() => { }}  >

            <Box style={{ width: 320, height: 400, padding: 20 }}>

                {!createAccount && <Typography variant="h5" style={{ paddingBottom: 10 }} >Login</Typography>}
                {createAccount && <Typography variant="h5" style={{ paddingBottom: 10 }} >Create a new Account</Typography>}

                <Formik
                    initialValues={{
                        username: getDefaultUserName(),
                        password: '',
                        confirm: '',
                        create: false
                    }}

                    validate={async values => {
                        const errors = {};
                        if (!values.username) {
                            errors.username = 'Required';
                        }
                        if (!values.password) {
                            errors.password = 'Required';
                        }
                        if (createAccount && values.password !== values.confirm) {
                            errors.confirm = 'Does not match password'
                        }
                        return errors;
                    }}

                    onSubmit={async (values, { setErrors, setFieldValue }) => {
                        // Reduce risk of clear text password logging
                        const password = await sha256Hash(values.password)

                        if (createAccount) {
                            try {
                                await store.createUser({ username: values.username, password: password })
                                setDefaultUserName(values.username)
                                setCreateAccount(false)
                                setFieldValue('confirm', '', false)
                            } catch (error) {
                                setFieldValue('password', '', false)
                                setFieldValue('confirm', '', false)
                                setErrors({ username: 'User already exist' })
                                return
                            }
                        }

                        try {
                            await store.connectUser({ username: values.username, password: password })
                            setDefaultUserName(values.username)
                        } catch (error) {
                            setFieldValue('password', '', false)
                            setErrors({ username: 'Invalid username or password' })
                        }
                    }}
                >

                    {({ submitForm, isSubmitting }) => (
                        <Form onKeyUp={handleEnter}>
                            {isSubmitting && <LinearProgress style={{ marginBottom: 10 }} />}
                            <Field
                                label="Username"
                                component={TextField}
                                name="username"
                                type="text"
                                fullWidth={true}
                            />
                            <br />
                            <Field
                                label="Password"
                                component={TextField}
                                type="password"
                                name="password"
                                fullWidth={true}
                            />
                            <br />
                            {createAccount && <Field
                                label="Confirm"
                                component={TextField}
                                type="password"
                                name="confirm"
                                fullWidth={true}
                            />}

                            <br />
                            <FormControlLabel style={{ position: 'absolute', top: 230 }}
                                label="Create a new account"
                                control={
                                    <Field component={Switch} type="checkbox" name="create" color='primary'
                                        onChange={e => setCreateAccount(e.target.checked)} />
                                }
                            />

                            <Box style={{ position: 'absolute', top: 270, left: 80 }}>
                                <Button
                                    id='OKButton'
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={submitForm}
                                    style={{ margin: 5, width: 80 }}
                                >OK</Button>

                                <Button
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={() => setShow(false)}
                                    style={{ margin: 5, width: 85 }}
                                >Cancel</Button>
                            </Box>

                            <Box style={{ position: 'absolute', top: 330, left: 20, width: 320 }}>
                                <hr />
                            </Box>

                            <Box style={{ position: 'absolute', top: 335, left: 20 }}>
                                <Typography variant="body2" style={{ marginTop: 20, marginBottom: 5, }}
                                >or login with:</Typography>

                                {!isDeveloperMode && <>
                                    <Button variant="outlined" color="primary" size="small"
                                        onClick={() => loginWith('Google')}
                                        style={{ marginLeft: 20, marginRight: 5, width: 85 }}
                                    >Google</Button>
                                    <Button variant="outlined" color="primary" size="small"
                                        onClick={() => loginWith('Facebook')}
                                        style={{ margin: 5, width: 85 }}
                                    >Facebook</Button>
                                    <Button variant="outlined" color="primary" size="small"
                                        onClick={() => loginWith('GitHub')}
                                        style={{ margin: 5, width: 85 }}
                                    >GitHub</Button>
                                </>}
                            </Box>
                        </Form>
                    )}
                </Formik>
            </Box>
        </Dialog >
    )
}

const getDefaultUserName = () => localStorage.getItem(usernameKey) ?? ''

const setDefaultUserName = name => localStorage.setItem(usernameKey, name)
